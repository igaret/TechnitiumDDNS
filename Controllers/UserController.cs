using DynamicDns.Models;
using DynamicDns.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DynamicDns.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var user = await _userService.RegisterUserAsync(model.Username, model.Email, model.Password);
                
                return Ok(new { 
                    message = "Registration successful. Please check your email to verify your account.",
                    userId = user.Id,
                    username = user.Username,
                    email = user.Email
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            try
            {
                var user = await _userService.AuthenticateAsync(model.UsernameOrEmail, model.Password);
                
                if (user == null)
                    return Unauthorized(new { error = "Invalid username/email or password" });

                if (!user.IsEmailVerified && !string.IsNullOrEmpty(user.EmailVerificationToken))
                    return BadRequest(new { error = "Email not verified. Please check your email for verification link." });

                if (!user.IsActive)
                    return BadRequest(new { error = "Account is disabled. Please contact support." });

                // Generate session token (in a real app, use JWT or similar)
                string token = Guid.NewGuid().ToString();

                return Ok(new {
                    token,
                    userId = user.Id,
                    username = user.Username,
                    email = user.Email,
                    plan = user.SubscriptionPlan,
                    isAdmin = user.IsAdmin
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                bool result = await _userService.VerifyEmailAsync(token);
                
                if (result)
                    return Ok(new { message = "Email verified successfully. You can now log in." });
                else
                    return BadRequest(new { error = "Invalid or expired verification token." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            try
            {
                await _userService.RequestPasswordResetAsync(model.Email);
                
                // Always return success to prevent email enumeration
                return Ok(new { message = "If your email is registered, you will receive a password reset link." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            try
            {
                bool result = await _userService.ResetPasswordAsync(model.Token, model.NewPassword);
                
                if (result)
                    return Ok(new { message = "Password reset successful. You can now log in with your new password." });
                else
                    return BadRequest(new { error = "Invalid or expired reset token." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
        {
            try
            {
                // In a real app, get userId from authenticated user
                int userId = model.UserId;
                
                bool result = await _userService.UpdateUserProfileAsync(userId, model.Email, model.CurrentPassword, model.NewPassword);
                
                if (result)
                    return Ok(new { message = "Profile updated successfully." });
                else
                    return BadRequest(new { error = "Failed to update profile. Please check your current password." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("api-key")]
        public async Task<IActionResult> CreateApiKey([FromBody] ApiKeyModel model)
        {
            try
            {
                // In a real app, get userId from authenticated user
                int userId = model.UserId;
                
                var apiKey = await _userService.CreateApiKeyAsync(userId, model.Name, model.AllowDomainManagement, model.AllowProfileManagement, model.AllowedIps);
                
                return Ok(new { 
                    message = "API key created successfully.",
                    apiKey = apiKey.Key,
                    name = apiKey.Name,
                    created = apiKey.CreatedOn
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("api-key/{id}")]
        public async Task<IActionResult> RevokeApiKey(int id, [FromQuery] int userId)
        {
            try
            {
                // In a real app, get userId from authenticated user
                bool result = await _userService.RevokeApiKeyAsync(userId, id);
                
                if (result)
                    return Ok(new { message = "API key revoked successfully." });
                else
                    return BadRequest(new { error = "Failed to revoke API key." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api-keys")]
        public async Task<IActionResult> GetApiKeys([FromQuery] int userId)
        {
            try
            {
                // In a real app, get userId from authenticated user
                var apiKeys = await _userService.GetApiKeysAsync(userId);
                
                return Ok(new { 
                    apiKeys = apiKeys.Select(k => new {
                        id = k.Id,
                        name = k.Name,
                        created = k.CreatedOn,
                        lastUsed = k.LastUsed,
                        isActive = k.IsActive,
                        allowDomainManagement = k.AllowDomainManagement,
                        allowProfileManagement = k.AllowProfileManagement,
                        allowedIps = k.AllowedIps
                    })
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class RegisterModel
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginModel
    {
        public string UsernameOrEmail { get; set; }
        public string Password { get; set; }
    }

    public class ForgotPasswordModel
    {
        public string Email { get; set; }
    }

    public class ResetPasswordModel
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }

    public class UpdateProfileModel
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class ApiKeyModel
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public bool AllowDomainManagement { get; set; }
        public bool AllowProfileManagement { get; set; }
        public string AllowedIps { get; set; }
    }
}