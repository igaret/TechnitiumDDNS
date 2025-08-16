using DynamicDns.Data;
using DynamicDns.Models;
using DnsServerCore.ApplicationCommon;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TechnitiumLibrary.Net.Dns;
using TechnitiumLibrary.Net.Dns.ResourceRecords;

namespace DynamicDns.Services
{
    public class DynamicDnsService
    {
        private readonly DynamicDnsDbContext _dbContext;
        private readonly IDnsServer _dnsServer;

        public DynamicDnsService(DynamicDnsDbContext dbContext, IDnsServer dnsServer)
        {
            _dbContext = dbContext;
            _dnsServer = dnsServer;
        }

        public async Task<DynamicDnsEntry> CreateDynamicDnsEntryAsync(int userId, string domainName, string zoneName = null)
        {
            // Check if domain already exists
            if (await _dbContext.DynamicDnsEntries.AnyAsync(d => d.DomainName == domainName))
                throw new Exception("Domain already exists");

            // Generate update token
            string updateToken = GenerateRandomToken();

            // If zoneName is not provided, try to determine it from the domain name
            if (string.IsNullOrEmpty(zoneName))
            {
                string[] parts = domainName.Split('.');
                if (parts.Length >= 2)
                {
                    zoneName = string.Join(".", parts.Skip(parts.Length - 2));
                }
                else
                {
                    zoneName = domainName;
                }
            }

            // Create record name (subdomain part)
            string recordName = domainName;
            if (domainName.EndsWith("." + zoneName))
            {
                recordName = domainName.Substring(0, domainName.Length - zoneName.Length - 1);
            }
            if (string.IsNullOrEmpty(recordName))
            {
                recordName = "@";
            }

            // Create new dynamic DNS entry
            var entry = new DynamicDnsEntry
            {
                DomainName = domainName,
                UpdateToken = updateToken,
                CreatedOn = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                IsActive = true,
                UserId = userId,
                ZoneName = zoneName,
                RecordName = recordName
            };

            _dbContext.DynamicDnsEntries.Add(entry);
            await _dbContext.SaveChangesAsync();

            return entry;
        }

        public async Task<bool> UpdateDynamicDnsEntryAsync(string domainName, string updateToken, string ipv4Address, string ipv6Address, string clientIp)
        {
            var entry = await _dbContext.DynamicDnsEntries
                .FirstOrDefaultAsync(d => d.DomainName == domainName && d.UpdateToken == updateToken && d.IsActive);

            if (entry == null)
                return false;

            // Update IP addresses
            bool updated = false;

            if (!string.IsNullOrEmpty(ipv4Address) && entry.IPv4Address != ipv4Address)
            {
                // Validate IPv4 address
                if (IPAddress.TryParse(ipv4Address, out IPAddress ipv4) && ipv4.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    entry.IPv4Address = ipv4Address;
                    updated = true;
                }
            }

            if (!string.IsNullOrEmpty(ipv6Address) && entry.IPv6Address != ipv6Address)
            {
                // Validate IPv6 address
                if (IPAddress.TryParse(ipv6Address, out IPAddress ipv6) && ipv6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    entry.IPv6Address = ipv6Address;
                    updated = true;
                }
            }

            // If client IP is provided but no specific IP addresses, use client IP
            if (string.IsNullOrEmpty(ipv4Address) && string.IsNullOrEmpty(ipv6Address) && !string.IsNullOrEmpty(clientIp))
            {
                if (IPAddress.TryParse(clientIp, out IPAddress ip))
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && entry.IPv4Address != clientIp)
                    {
                        entry.IPv4Address = clientIp;
                        updated = true;
                    }
                    else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && entry.IPv6Address != clientIp)
                    {
                        entry.IPv6Address = clientIp;
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                entry.LastUpdated = DateTime.UtcNow;
                entry.UpdateCount++;
                entry.LastUpdateAttempt = DateTime.UtcNow;
                entry.LastUpdateStatus = "Success";
                entry.LastUpdateIp = clientIp;

                await _dbContext.SaveChangesAsync();

                // Update DNS cache to ensure changes take effect immediately
                try
                {
                    if (!string.IsNullOrEmpty(entry.IPv4Address))
                    {
                        var question = new DnsQuestionRecord(entry.DomainName, DnsResourceRecordType.A, DnsClass.IN);
                        var answer = new DnsResourceRecord(entry.DomainName, DnsResourceRecordType.A, DnsClass.IN, 60, new DnsARecordData(IPAddress.Parse(entry.IPv4Address)));
                        _dnsServer.DnsCache.CacheRecord(answer, false);
                    }

                    if (!string.IsNullOrEmpty(entry.IPv6Address))
                    {
                        var question = new DnsQuestionRecord(entry.DomainName, DnsResourceRecordType.AAAA, DnsClass.IN);
                        var answer = new DnsResourceRecord(entry.DomainName, DnsResourceRecordType.AAAA, DnsClass.IN, 60, new DnsAAAARecordData(IPAddress.Parse(entry.IPv6Address)));
                        _dnsServer.DnsCache.CacheRecord(answer, false);
                    }
                }
                catch (Exception ex)
                {
                    _dnsServer.WriteLog($"DynamicDnsApp: Error updating DNS cache: {ex.Message}");
                }
            }
            else
            {
                entry.LastUpdateAttempt = DateTime.UtcNow;
                entry.LastUpdateStatus = "No changes";
                entry.LastUpdateIp = clientIp;
                await _dbContext.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeleteDynamicDnsEntryAsync(int userId, int entryId)
        {
            var entry = await _dbContext.DynamicDnsEntries
                .FirstOrDefaultAsync(d => d.Id == entryId && d.UserId == userId);

            if (entry == null)
                return false;

            _dbContext.DynamicDnsEntries.Remove(entry);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<DynamicDnsEntry> GetDynamicDnsEntryAsync(int userId, int entryId)
        {
            return await _dbContext.DynamicDnsEntries
                .FirstOrDefaultAsync(d => d.Id == entryId && d.UserId == userId);
        }

        public async Task<DynamicDnsEntry> GetDynamicDnsEntryByDomainAsync(string domainName)
        {
            return await _dbContext.DynamicDnsEntries
                .FirstOrDefaultAsync(d => d.DomainName == domainName && d.IsActive);
        }

        public async Task<DynamicDnsEntry> GetDynamicDnsEntryByTokenAsync(string updateToken)
        {
            return await _dbContext.DynamicDnsEntries
                .FirstOrDefaultAsync(d => d.UpdateToken == updateToken && d.IsActive);
        }

        public async Task<List<DynamicDnsEntry>> GetUserDynamicDnsEntriesAsync(int userId)
        {
            return await _dbContext.DynamicDnsEntries
                .Where(d => d.UserId == userId)
                .OrderBy(d => d.DomainName)
                .ToListAsync();
        }

        public async Task<bool> RegenerateUpdateTokenAsync(int userId, int entryId)
        {
            var entry = await _dbContext.DynamicDnsEntries
                .FirstOrDefaultAsync(d => d.Id == entryId && d.UserId == userId);

            if (entry == null)
                return false;

            entry.UpdateToken = GenerateRandomToken();
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetUserDomainCountAsync(int userId)
        {
            return await _dbContext.DynamicDnsEntries
                .CountAsync(d => d.UserId == userId);
        }

        public async Task<bool> CanUserAddMoreDomainsAsync(int userId, AppConfig config)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return false;

            int currentCount = await GetUserDomainCountAsync(userId);
            
            switch (user.SubscriptionPlan.ToLower())
            {
                case "free":
                    return config.FreeTierEnabled && currentCount < config.FreeTierMaxDomains;
                
                case "basic":
                    return currentCount < config.BasicPlanMaxDomains;
                
                case "pro":
                    return currentCount < config.ProPlanMaxDomains;
                
                default:
                    return false;
            }
        }

        #region Helper Methods

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