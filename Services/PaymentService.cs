using DynamicDns.Data;
using DynamicDns.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DynamicDns.Services
{
    public class PaymentService
    {
        private readonly DynamicDnsDbContext _dbContext;
        private readonly string _stripeApiKey;
        private bool _isStripeConfigured;

        public PaymentService(DynamicDnsDbContext dbContext, string stripeApiKey)
        {
            _dbContext = dbContext;
            _stripeApiKey = stripeApiKey;
            _isStripeConfigured = !string.IsNullOrEmpty(stripeApiKey);

            if (_isStripeConfigured)
            {
                StripeConfiguration.ApiKey = stripeApiKey;
            }
        }

        public bool IsPaymentEnabled => _isStripeConfigured;

        public async Task<string> CreateCheckoutSessionAsync(int userId, string plan, string successUrl, string cancelUrl)
        {
            if (!_isStripeConfigured)
                throw new Exception("Payment system is not configured");

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            // Determine price based on plan
            string priceId;
            switch (plan.ToLower())
            {
                case "basic":
                    priceId = "price_basic"; // Replace with actual Stripe price ID
                    break;
                case "pro":
                    priceId = "price_pro"; // Replace with actual Stripe price ID
                    break;
                default:
                    throw new Exception("Invalid plan selected");
            }

            // Create or retrieve Stripe customer
            string customerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(customerId))
            {
                var customerOptions = new CustomerCreateOptions
                {
                    Email = user.Email,
                    Name = user.Username,
                    Metadata = new Dictionary<string, string>
                    {
                        { "UserId", user.Id.ToString() }
                    }
                };

                var customerService = new CustomerService();
                var customer = await customerService.CreateAsync(customerOptions);
                customerId = customer.Id;

                // Save customer ID to user
                user.StripeCustomerId = customerId;
                await _dbContext.SaveChangesAsync();
            }

            // Create checkout session
            var options = new SessionCreateOptions
            {
                Customer = customerId,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1,
                    },
                },
                Mode = "subscription",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "UserId", user.Id.ToString() },
                    { "Plan", plan }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return session.Id;
        }

        public async Task<bool> ProcessWebhookAsync(string json, string stripeSignature, string webhookSecret)
        {
            if (!_isStripeConfigured)
                throw new Exception("Payment system is not configured");

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);

                // Handle the event
                switch (stripeEvent.Type)
                {
                    case Events.CheckoutSessionCompleted:
                        var session = stripeEvent.Data.Object as Session;
                        await HandleCheckoutSessionCompletedAsync(session);
                        break;

                    case Events.InvoicePaid:
                        var invoice = stripeEvent.Data.Object as Invoice;
                        await HandleInvoicePaidAsync(invoice);
                        break;

                    case Events.CustomerSubscriptionDeleted:
                        var subscription = stripeEvent.Data.Object as Subscription;
                        await HandleSubscriptionDeletedAsync(subscription);
                        break;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> CancelSubscriptionAsync(int userId)
        {
            if (!_isStripeConfigured)
                throw new Exception("Payment system is not configured");

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.StripeSubscriptionId))
                return false;

            try
            {
                var service = new SubscriptionService();
                await service.CancelAsync(user.StripeSubscriptionId, new SubscriptionCancelOptions());

                // Update user subscription status
                user.SubscriptionPlan = "Free";
                user.SubscriptionExpiry = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<PaymentTransaction>> GetUserTransactionsAsync(int userId)
        {
            return await _dbContext.PaymentTransactions
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.TransactionDate)
                .ToListAsync();
        }

        #region Private Methods

        private async Task HandleCheckoutSessionCompletedAsync(Session session)
        {
            if (session == null || !session.Metadata.TryGetValue("UserId", out string userIdStr) || !int.TryParse(userIdStr, out int userId))
                return;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return;

            // Update user subscription
            if (session.Metadata.TryGetValue("Plan", out string plan))
            {
                user.SubscriptionPlan = plan;
                user.StripeSubscriptionId = session.Subscription;
                user.SubscriptionExpiry = DateTime.UtcNow.AddMonths(1); // Default to 1 month
                await _dbContext.SaveChangesAsync();
            }

            // Create transaction record
            var transaction = new PaymentTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                UserId = userId,
                TransactionDate = DateTime.UtcNow,
                Amount = session.AmountTotal.HasValue ? (decimal)session.AmountTotal.Value / 100 : 0, // Convert from cents
                Currency = session.Currency?.ToUpper(),
                Status = "Completed",
                PaymentMethod = "Card",
                SubscriptionPlan = plan,
                Description = $"Subscription to {plan} plan",
                StripeCustomerId = session.CustomerId,
                StripeSubscriptionId = session.Subscription
            };

            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();
        }

        private async Task HandleInvoicePaidAsync(Invoice invoice)
        {
            if (invoice == null || string.IsNullOrEmpty(invoice.CustomerId) || string.IsNullOrEmpty(invoice.SubscriptionId))
                return;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == invoice.CustomerId);
            if (user == null)
                return;

            // Update subscription expiry
            user.SubscriptionExpiry = DateTime.UtcNow.AddMonths(1); // Default to 1 month
            await _dbContext.SaveChangesAsync();

            // Create transaction record
            var transaction = new PaymentTransaction
            {
                TransactionId = invoice.Id,
                UserId = user.Id,
                TransactionDate = DateTime.UtcNow,
                Amount = invoice.AmountPaid / 100, // Convert from cents
                Currency = invoice.Currency?.ToUpper(),
                Status = "Completed",
                PaymentMethod = "Card",
                SubscriptionPlan = user.SubscriptionPlan,
                Description = $"Renewal of {user.SubscriptionPlan} plan",
                StripeCustomerId = invoice.CustomerId,
                StripeSubscriptionId = invoice.SubscriptionId
            };

            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();
        }

        private async Task HandleSubscriptionDeletedAsync(Subscription subscription)
        {
            if (subscription == null || string.IsNullOrEmpty(subscription.CustomerId))
                return;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == subscription.CustomerId);
            if (user == null)
                return;

            // Update user subscription
            user.SubscriptionPlan = "Free";
            user.SubscriptionExpiry = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        #endregion
    }
}