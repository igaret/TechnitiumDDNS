using DynamicDns.Models;
using DynamicDns.Services;
using DnsServerCore.ApplicationCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DynamicDns.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DynamicDnsController : ControllerBase
    {
        private readonly DynamicDnsService _dynamicDnsService;
        private readonly UserService _userService;
        private readonly AppConfig _config;
        private readonly IDnsServer _dnsServer;

        public DynamicDnsController(DynamicDnsService dynamicDnsService, UserService userService, AppConfig config, IDnsServer dnsServer)
        {
            _dynamicDnsService = dynamicDnsService;
            _userService = userService;
            _config = config;
            _dnsServer = dnsServer;
        }

        [HttpGet("update")]
        public async Task<IActionResult> Update([FromQuery] string domain, [FromQuery] string token, 
            [FromQuery] string ipv4 = null, [FromQuery] string ipv6 = null)
        {
            try
            {
                // Get client IP
                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                
                bool result = await _dynamicDnsService.UpdateDynamicDnsEntryAsync(domain, token, ipv4, ipv6, clientIp);
                
                if (result)
                    return Ok(new { message = "DNS record updated successfully." });
                else
                    return BadRequest(new { error = "Invalid domain or token." });
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog($"DynamicDnsApp: Error updating DNS: {ex.Message}");
                return BadRequest(new { error = "An error occurred while updating DNS record." });
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateDomainModel model)
        {
            try
            {
                // In a real app, get userId from authenticated user
                int userId = model.UserId;
                
                // Check if user can add more domains
                bool canAdd = await _dynamicDnsService.CanUserAddMoreDomainsAsync(userId, _config);
                if (!canAdd)
                {
                    return BadRequest(new { error = "You have reached the maximum number of domains allowed for your subscription plan." });
                }
                
                var entry = await _dynamicDnsService.CreateDynamicDnsEntryAsync(userId, model.DomainName, model.ZoneName);
                
                return Ok(new { 
                    message = "Domain created successfully.",
                    domain = entry.DomainName,
                    updateToken = entry.UpdateToken,
                    updateUrl = $"/api/dynamicdns/update?domain={entry.DomainName}&token={entry.UpdateToken}"
                });
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog($"DynamicDnsApp: Error creating domain: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] int userId)
        {
            try
            {
                // In a real app, get userId from authenticated user
                bool result = await _dynamicDnsService.DeleteDynamicDnsEntryAsync(userId, id);
                
                if (result)
                    return Ok(new { message = "Domain deleted successfully." });
                else
                    return BadRequest(new { error = "Failed to delete domain." });
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog($"DynamicDnsApp: Error deleting domain: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("list")]
        public async Task<IActionResult> List([FromQuery] int userId)
        {
            try
            {
                // In a real app, get userId from authenticated user
                var entries = await _dynamicDnsService.GetUserDynamicDnsEntriesAsync(userId);
                
                return Ok(new { 
                    domains = entries.Select(e => new {
                        id = e.Id,
                        domain = e.DomainName,
                        ipv4 = e.IPv4Address,
                        ipv6 = e.IPv6Address,
                        created = e.CreatedOn,
                        lastUpdated = e.LastUpdated,
                        updateToken = e.UpdateToken,
                        isActive = e.IsActive,
                        updateUrl = $"/api/dynamicdns/update?domain={e.DomainName}&token={e.UpdateToken}",
                        updateCount = e.UpdateCount,
                        lastUpdateStatus = e.LastUpdateStatus,
                        lastUpdateAttempt = e.LastUpdateAttempt,
                        notes = e.Notes
                    })
                });
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog($"DynamicDnsApp: Error listing domains: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("regenerate-token/{id}")]
        public async Task<IActionResult> RegenerateToken(int id, [FromQuery] int userId)
        {
            try
            {
                // In a real app, get userId from authenticated user
                bool result = await _dynamicDnsService.RegenerateUpdateTokenAsync(userId, id);
                
                if (result)
                {
                    var entry = await _dynamicDnsService.GetDynamicDnsEntryAsync(userId, id);
                    return Ok(new { 
                        message = "Token regenerated successfully.",
                        updateToken = entry.UpdateToken,
                        updateUrl = $"/api/dynamicdns/update?domain={entry.DomainName}&token={entry.UpdateToken}"
                    });
                }
                else
                {
                    return BadRequest(new { error = "Failed to regenerate token." });
                }
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog($"DynamicDnsApp: Error regenerating token: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("client-ip")]
        public IActionResult GetClientIp()
        {
            try
            {
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                bool isIpv6 = HttpContext.Connection.RemoteIpAddress?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
                
                return Ok(new { 
                    ip = ipAddress,
                    isIpv6 = isIpv6
                });
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog($"DynamicDnsApp: Error getting client IP: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class CreateDomainModel
    {
        public int UserId { get; set; }
        public string DomainName { get; set; }
        public string ZoneName { get; set; }
    }
}