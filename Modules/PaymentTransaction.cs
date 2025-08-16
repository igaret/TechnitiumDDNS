using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicDns.Models
{
    public class PaymentTransaction
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string TransactionId { get; set; }
        
        [ForeignKey("User")]
        public int UserId { get; set; }
        
        public virtual User User { get; set; }
        
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        
        [Column(TypeName = "decimal(10, 2)")]
        public decimal Amount { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Currency { get; set; } = "USD";
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; }
        
        [MaxLength(50)]
        public string PaymentMethod { get; set; }
        
        [MaxLength(50)]
        public string SubscriptionPlan { get; set; }
        
        [MaxLength(255)]
        public string Description { get; set; }
        
        [MaxLength(100)]
        public string StripePaymentIntentId { get; set; }
        
        [MaxLength(100)]
        public string StripeCustomerId { get; set; }
        
        [MaxLength(100)]
        public string StripeSubscriptionId { get; set; }
        
        [MaxLength(255)]
        public string Notes { get; set; }
        
        public bool IsRefunded { get; set; } = false;
        
        public DateTime? RefundDate { get; set; }
        
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? RefundAmount { get; set; }
    }
}