using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DynamicDns.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Username { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Email { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string PasswordHash { get; set; }
        
        [MaxLength(100)]
        public string Salt { get; set; }
        
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
        
        public bool IsEmailVerified { get; set; } = false;
        
        [MaxLength(100)]
        public string EmailVerificationToken { get; set; }
        
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        
        [MaxLength(100)]
        public string ResetPasswordToken { get; set; }
        
        public DateTime? ResetPasswordTokenExpiry { get; set; }
        
        [MaxLength(50)]
        public string SubscriptionPlan { get; set; } = "Free";
        
        public DateTime? SubscriptionExpiry { get; set; }
        
        [MaxLength(100)]
        public string StripeCustomerId { get; set; }
        
        [MaxLength(100)]
        public string StripeSubscriptionId { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public bool IsAdmin { get; set; } = false;
        
        public virtual ICollection<DynamicDnsEntry> DynamicDnsEntries { get; set; } = new List<DynamicDnsEntry>();
        
        public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
        
        public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
    }
}