using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using wsOrderService.Data;
using wsOrderService.Messaging;
using wsOrderService.Models;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RabbitMQProducer _producer;

        public OrdersController(ApplicationDbContext context, RabbitMQProducer producer)
        {
            _context = context;
            _producer = producer;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] Order order)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (userEmail == null)
            {
                return Unauthorized("Invalid token.");
            }

            order.CreatedBy = userEmail;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            await _producer.PublishOrderCreatedAsync(order.Id, "Pending");

            return Ok(order);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyOrders()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (userEmail == null)
            {
                return Unauthorized("Invalid token.");
            }

            var orders = await _context.Orders
                .Where(o => o.CreatedBy == userEmail)
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders.ToListAsync();
            return Ok(orders);
        }

        [HttpPut("{id}/assign-courier")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignCourier(int id, [FromBody] string courierEmail)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            if (string.IsNullOrEmpty(courierEmail))
            {
                return BadRequest("Courier email must be provided.");
            }

            order.AssignedCourier = courierEmail;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Courier assigned successfully.", order });
        }

        [HttpPut("{id}/update-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            if (string.IsNullOrEmpty(status))
            {
                return BadRequest("Status must be provided.");
            }

            order.Status = status;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Order status updated successfully.", order });
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            if (userRole != "Admin" && order.CreatedBy != userEmail)
            {
                return Forbid("You don't have permission to cancel this order.");
            }

            if (order.Status == "Canceled")
            {
                return BadRequest("Order is already canceled.");
            }

            order.Status = "Canceled";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Order canceled successfully.", order });
        }
    }
}
