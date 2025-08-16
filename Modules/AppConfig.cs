using System;

namespace DynamicDns.Models
{
    public class AppConfig
    {
        public int WebServicePort { get; set; } = 8080;
        public string DatabasePath { get; set; } = "dynamicdns.db";
        public string StripeApiKey { get; set; } = "";
        public string StripeWebhookSecret { get; set; } = "";
        public bool FreeTierEnabled { get; set; } = true;
        public int FreeTierMaxDomains { get; set; } = 3;
        public double BasicPlanPrice { get; set; } = 5.99;
        public double ProPlanPrice { get; set; } = 9.99;
        public int BasicPlanMaxDomains { get; set; } = 10;
        public int ProPlanMaxDomains { get; set; } = 50;
        public int UpdateIntervalMinutes { get; set; } = 5;
        public bool RequireEmailVerification { get; set; } = true;
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public string SmtpFromEmail { get; set; } = "noreply@example.com";
        public bool SmtpUseSsl { get; set; } = true;
    }

    public class DynamicDnsAppRecordData
    {
        public string[] AllowedNetworks { get; set; } = new string[] { "0.0.0.0/0", "::/0" };
        public bool RequireAuth { get; set; } = true;
    }
}