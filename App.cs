/*
Dynamic DNS App for Technitium DNS Server
Copyright (C)  Garet McCallister

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using DnsServerCore.ApplicationCommon;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TechnitiumLibrary.Net.Dns;
using TechnitiumLibrary.Net.Dns.ResourceRecords;
using DynamicDns.Models;
using DynamicDns.Services;
using DynamicDns.Data;
using Microsoft.EntityFrameworkCore;

namespace DynamicDns
{
    public sealed class App : IDnsApplication, IDnsAppRecordRequestHandler, IDisposable
    {
        #region variables

        private IDnsServer _dnsServer;
        private string _configFolder;
        private AppConfig _config;
        private IWebHost _webService;
        private CancellationTokenSource _webServiceCancellationTokenSource;
        private DynamicDnsDbContext _dbContext;
        private UserService _userService;
        private DynamicDnsService _dynamicDnsService;
        private PaymentService _paymentService;

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_webService != null)
            {
                if (_webServiceCancellationTokenSource != null)
                {
                    _webServiceCancellationTokenSource.Cancel();
                    _webServiceCancellationTokenSource = null;
                }

                _webService.Dispose();
                _webService = null;
            }

            if (_dbContext != null)
            {
                _dbContext.Dispose();
                _dbContext = null;
            }
        }

        #endregion

        #region private

        private void LoadConfiguration(string config)
        {
            try
            {
                if (string.IsNullOrEmpty(config))
                {
                    // Load default configuration
                    _config = new AppConfig();
                }
                else
                {
                    // Deserialize JSON configuration
                    _config = JsonSerializer.Deserialize<AppConfig>(config);
                }

                // Ensure configuration has valid values
                if (_config.WebServicePort < 1)
                    _config.WebServicePort = 5380; // Default port

                if (string.IsNullOrEmpty(_config.DatabasePath))
                    _config.DatabasePath = Path.Combine(_configFolder, "dynamicdns.db");

                if (string.IsNullOrEmpty(_config.StripeApiKey))
                    _config.StripeApiKey = ""; // Default empty, will disable payment features if not set
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog("DynamicDnsApp: Error loading configuration: " + ex.ToString());
                _config = new AppConfig();
            }
        }

        private async Task InitializeDatabase()
        {
            try
            {
                // Ensure database directory exists
                string dbDirectory = Path.GetDirectoryName(_config.DatabasePath);
                if (!Directory.Exists(dbDirectory))
                    Directory.CreateDirectory(dbDirectory);

                // Create DB context options
                var optionsBuilder = new DbContextOptionsBuilder<DynamicDnsDbContext>();
                optionsBuilder.UseSqlite($"Data Source={_config.DatabasePath}");

                // Create and migrate database
                _dbContext = new DynamicDnsDbContext(optionsBuilder.Options);
                await _dbContext.Database.MigrateAsync();

                // Initialize services
                _userService = new UserService(_dbContext);
                _dynamicDnsService = new DynamicDnsService(_dbContext, _dnsServer);
                _paymentService = new PaymentService(_dbContext, _config.StripeApiKey);
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog("DynamicDnsApp: Error initializing database: " + ex.ToString());
                throw;
            }
        }

        private async Task StartWebService()
        {
            try
            {
                _webServiceCancellationTokenSource = new CancellationTokenSource();

                _webService = new WebHostBuilder()
                    .UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Any, _config.WebServicePort);
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddControllers();
                        services.AddSingleton(_dnsServer);
                        services.AddSingleton(_dbContext);
                        services.AddSingleton(_userService);
                        services.AddSingleton(_dynamicDnsService);
                        services.AddSingleton(_paymentService);
                        services.AddSingleton(_config);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        
                        // Serve static files from web folder
                        string webFolder = Path.Combine(_dnsServer.ApplicationFolder, "web");
                        if (Directory.Exists(webFolder))
                        {
                            app.UseStaticFiles(new StaticFileOptions
                            {
                                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webFolder),
                                RequestPath = ""
                            });
                        }

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            
                            // Default route to index.html
                            endpoints.MapGet("/", async context =>
                            {
                                string indexPath = Path.Combine(webFolder, "index.html");
                                if (File.Exists(indexPath))
                                {
                                    context.Response.ContentType = "text/html";
                                    await context.Response.SendFileAsync(indexPath);
                                }
                                else
                                {
                                    context.Response.StatusCode = 404;
                                    await context.Response.WriteAsync("Dynamic DNS App");
                                }
                            });
                        });
                    })
                    .Build();

                await _webService.StartAsync(_webServiceCancellationTokenSource.Token);
                _dnsServer.WriteLog($"DynamicDnsApp: Web service started on port {_config.WebServicePort}");
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog("DynamicDnsApp: Failed to start web service: " + ex.ToString());
            }
        }

        #endregion

        #region public

        public async Task InitializeAsync(IDnsServer dnsServer, string config)
        {
            _dnsServer = dnsServer;
            _configFolder = dnsServer.ApplicationFolder;

            // Load configuration
            LoadConfiguration(config);

            // Initialize database
            await InitializeDatabase();

            // Start web service
            await StartWebService();

            _dnsServer.WriteLog("DynamicDnsApp: Initialized successfully");
        }

        public Task<DnsDatagram> ProcessRequestAsync(DnsDatagram request, IPEndPoint remoteEP, DnsTransportProtocol protocol, bool isRecursionAllowed, string zoneName, string appRecordName, uint appRecordTtl, string appRecordData)
        {
            try
            {
                DnsQuestionRecord question = request.Question[0];

                // Check if this is a request for our dynamic DNS domain
                if (!question.Name.Equals(appRecordName, StringComparison.OrdinalIgnoreCase) && !appRecordName.StartsWith('*'))
                    return Task.FromResult<DnsDatagram>(null);

                // Parse app record data (if any)
                DynamicDnsAppRecordData recordData = null;
                if (!string.IsNullOrEmpty(appRecordData))
                {
                    try
                    {
                        recordData = JsonSerializer.Deserialize<DynamicDnsAppRecordData>(appRecordData);
                    }
                    catch
                    {
                        // Invalid app record data, use defaults
                        recordData = new DynamicDnsAppRecordData();
                    }
                }
                else
                {
                    recordData = new DynamicDnsAppRecordData();
                }

                // Check if the domain is registered in our system
                string domainName = question.Name.ToLower();
                var dynamicDnsEntry = _dynamicDnsService.GetDynamicDnsEntryByDomainAsync(domainName).GetAwaiter().GetResult();

                if (dynamicDnsEntry == null)
                {
                    // Domain not registered, return null to let DNS server handle it
                    return Task.FromResult<DnsDatagram>(null);
                }

                // Process based on record type
                DnsResourceRecord answer;

                switch (question.Type)
                {
                    case DnsResourceRecordType.A:
                        if (string.IsNullOrEmpty(dynamicDnsEntry.IPv4Address))
                            return Task.FromResult<DnsDatagram>(null);

                        answer = new DnsResourceRecord(question.Name, DnsResourceRecordType.A, DnsClass.IN, appRecordTtl, 
                            new DnsARecordData(IPAddress.Parse(dynamicDnsEntry.IPv4Address)));
                        break;

                    case DnsResourceRecordType.AAAA:
                        if (string.IsNullOrEmpty(dynamicDnsEntry.IPv6Address))
                            return Task.FromResult<DnsDatagram>(null);

                        answer = new DnsResourceRecord(question.Name, DnsResourceRecordType.AAAA, DnsClass.IN, appRecordTtl, 
                            new DnsAAAARecordData(IPAddress.Parse(dynamicDnsEntry.IPv6Address)));
                        break;

                    case DnsResourceRecordType.TXT:
                        string txtValue = $"Updated: {dynamicDnsEntry.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss")}";
                        answer = new DnsResourceRecord(question.Name, DnsResourceRecordType.TXT, DnsClass.IN, appRecordTtl, 
                            new DnsTXTRecordData(txtValue));
                        break;

                    default:
                        return Task.FromResult<DnsDatagram>(null);
                }

                return Task.FromResult(new DnsDatagram(request.Identifier, true, request.OPCODE, true, false, request.RecursionDesired, isRecursionAllowed, false, false, DnsResponseCode.NoError, request.Question, new DnsResourceRecord[] { answer }));
            }
            catch (Exception ex)
            {
                _dnsServer.WriteLog("DynamicDnsApp: Error processing request: " + ex.ToString());
                return Task.FromResult<DnsDatagram>(null);
            }
        }

        #endregion

        #region properties

        public string Description
        { get { return "Dynamic DNS service with user registration and payment system."; } }

        public string ApplicationRecordDataTemplate
        { 
            get 
            { 
                return @"{
  ""allowedNetworks"": [""0.0.0.0/0"", ""::/0""],
  ""requireAuth"": true
}"; 
            } 
        }

        #endregion
    }
}
