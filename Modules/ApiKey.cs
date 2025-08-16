using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicDns.Models
{
    public class ApiKey
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Key { get; set; }
        
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        
        public DateTime? ExpiresOn { get; set; }
        
        public DateTime? LastUsed { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [ForeignKey("User")]
        public int UserId { get; set; }
        
        public virtual User User { get; set; }
        
        [MaxLength(255)]
        public string AllowedIps { get; set; }
        
        public bool AllowDomainManagement { get; set; } = false;
        
        public bool AllowProfileManagement { get; set; } = false;
    }
}