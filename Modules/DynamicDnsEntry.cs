using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicDns.Models
{
    public class DynamicDnsEntry
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string DomainName { get; set; }
        
        [MaxLength(45)]
        public string IPv4Address { get; set; }
        
        [MaxLength(45)]
        public string IPv6Address { get; set; }
        
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        [MaxLength(100)]
        public string UpdateToken { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [ForeignKey("User")]
        public int UserId { get; set; }
        
        public virtual User User { get; set; }
        
        public string ZoneName { get; set; }
        
        public string RecordName { get; set; }
        
        [MaxLength(255)]
        public string Notes { get; set; }
        
        public int UpdateCount { get; set; } = 0;
        
        public DateTime? LastUpdateAttempt { get; set; }
        
        [MaxLength(255)]
        public string LastUpdateStatus { get; set; }
        
        [MaxLength(45)]
        public string LastUpdateIp { get; set; }
    }
}