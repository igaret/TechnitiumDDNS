using DynamicDns.Data;
using DynamicDns.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DynamicDns.Services
{
    public class UserService
    {
        private readonly DynamicDnsDbContext _dbContext;

        public UserService(DynamicDnsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User> RegisterUserAsync(string username, string email, string password)
        {
            // Check if username or email already exists
            if (await _dbContext.Users.AnyAsync(u => u.Username == username))
                throw new Exception("Username already exists");

            if (await _dbContext.Users.AnyAsync(u => u.Email == email))
                throw new Exception("Email already exists");

            // Generate salt and hash password
            string salt = GenerateRandomSalt();
            string passwordHash = HashPassword(password, salt);

            // Create verification token
            string verificationToken = GenerateRandomToken();

            // Create new user
            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                Salt = salt,
                CreatedOn = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,
                IsEmailVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddDays(3),
                SubscriptionPlan = "Free",
                IsActive = true
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            return user;
        }

        public async Task<User> AuthenticateAsync(string usernameOrEmail, string password)
        {
            // Find user by username or email
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);

            if (user == null)
                return null;

            // Verify password
            string passwordHash = HashPassword(password, user.Salt);
            if (passwordHash != user.PasswordHash)
                return null;

            // Update last login
            user.LastLogin = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return user;
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == token && 
                                         u.EmailVerificationTokenExpiry > DateTime.UtcNow);

            if (user == null)
                return false;

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return false;

            // Generate reset token
            string resetToken = GenerateRandomToken();
            user.ResetPasswordToken = resetToken;
            user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddHours(24);

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.ResetPasswordToken == token && 
                                         u.ResetPasswordTokenExpiry > DateTime.UtcNow);

            if (user == null)
                return false;

            // Update password
            string salt = GenerateRandomSalt();
            string passwordHash = HashPassword(newPassword, salt);

            user.PasswordHash = passwordHash;
            user.Salt = salt;
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiry = null;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<User> GetUserByIdAsync(int userId)
        {
            return await _dbContext.Users.FindAsync(userId);
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<bool> UpdateUserProfileAsync(int userId, string email, string currentPassword, string newPassword)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Verify current password if provided
            if (!string.IsNullOrEmpty(currentPassword))
            {
                string currentPasswordHash = HashPassword(currentPassword, user.Salt);
                if (currentPasswordHash != user.PasswordHash)
                    return false;
            }

            // Update email if provided
            if (!string.IsNullOrEmpty(email) && email != user.Email)
            {
                // Check if email is already in use
                if (await _dbContext.Users.AnyAsync(u => u.Email == email && u.Id != userId))
                    throw new Exception("Email already in use");

                user.Email = email;
                user.IsEmailVerified = false;
                user.EmailVerificationToken = GenerateRandomToken();
                user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddDays(3);
            }

            // Update password if provided
            if (!string.IsNullOrEmpty(newPassword))
            {
                string salt = GenerateRandomSalt();
                string passwordHash = HashPassword(newPassword, salt);

                user.PasswordHash = passwordHash;
                user.Salt = salt;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<ApiKey> CreateApiKeyAsync(int userId, string name, bool allowDomainManagement, bool allowProfileManagement, string allowedIps = null)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            string key = GenerateRandomToken();

            var apiKey = new ApiKey
            {
                Name = name,
                Key = key,
                CreatedOn = DateTime.UtcNow,
                IsActive = true,
                UserId = userId,
                AllowedIps = allowedIps,
                AllowDomainManagement = allowDomainManagement,
                AllowProfileManagement = allowProfileManagement
            };

            _dbContext.ApiKeys.Add(apiKey);
            await _dbContext.SaveChangesAsync();

            return apiKey;
        }

        public async Task<bool> RevokeApiKeyAsync(int userId, int apiKeyId)
        {
            var apiKey = await _dbContext.ApiKeys
                .FirstOrDefaultAsync(a => a.Id == apiKeyId && a.UserId == userId);

            if (apiKey == null)
                return false;

            apiKey.IsActive = false;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<ApiKey>> GetApiKeysAsync(int userId)
        {
            return await _dbContext.ApiKeys
                .Where(a => a.UserId == userId)
                .ToListAsync();
        }

        public async Task<ApiKey> ValidateApiKeyAsync(string key, string ipAddress)
        {
            var apiKey = await _dbContext.ApiKeys
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Key == key && a.IsActive);

            if (apiKey == null)
                return null;

            // Check if key is expired
            if (apiKey.ExpiresOn.HasValue && apiKey.ExpiresOn.Value < DateTime.UtcNow)
                return null;

            // Check if IP is allowed
            if (!string.IsNullOrEmpty(apiKey.AllowedIps))
            {
                var allowedIps = apiKey.AllowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (!allowedIps.Contains(ipAddress))
                    return null;
            }

            // Update last used
            apiKey.LastUsed = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return apiKey;
        }

        #region Helper Methods

        private string GenerateRandomSalt()
        {
            byte[] saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password + salt);
                byte[] hashBytes = sha256.ComputeHash(passwordBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private string GenerateRandomToken()
        {
            byte[] tokenBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            return Convert.ToBase64String(tokenBytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "")
                .Substring(0, 32);
        }

        #endregion
    }
}