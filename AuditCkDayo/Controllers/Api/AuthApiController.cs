using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;

namespace AuditCkDayo.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly AuditDbContext _context;
        private readonly JwtTokenService _jwt;

        public AuthApiController(AuditDbContext context, JwtTokenService jwt)
        {
            _context = context;
            _jwt = jwt;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email and password are required." });
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Establishment)
                .FirstOrDefaultAsync(u => u.Email == request.Email.Trim() && !u.IsDeleted);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            var token = _jwt.GenerateToken(user);
            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    Role = user.Role.ToString(),
                    user.EstablishmentId,
                    BranchName = user.Establishment?.Name
                }
            });
        }

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Establishment)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                user.Id,
                user.Name,
                user.Email,
                Role = user.Role.ToString(),
                user.EstablishmentId,
                BranchName = user.Establishment?.Name,
                user.PcfBalance,
                user.DailyStartingFloat
            });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
