using DynamicDns.Models;
using DynamicDns.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DynamicDns.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly PaymentService _paymentService;
        private readonly AppConfig _config;

        public PaymentController(PaymentService paymentService, AppConfig config)
        {
            _paymentService = paymentService;
            _config = config;
        }

        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout([FromBody] CheckoutModel model)
        {
            try
            {
                if (!_paymentService.IsPaymentEnabled)
                    return BadRequest(new { error = "Payment system is not configured." });

                // In a real app, get userId from authenticated user
                int userId = model.UserId;
                
                string sessionId = await _paymentService.CreateCheckoutSessionAsync(
                    userId, 
                    model.Plan, 
                    model.SuccessUrl, 
                    model.CancelUrl);
                
                return Ok(new { sessionId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                if (!_paymentService.IsPaymentEnabled)
                    return BadRequest(new { error = "Payment system is not configured." });

                if (string.IsNullOrEmpty(_config.StripeWebhookSecret))
                    return BadRequest(new { error = "Webhook secret is not configured." });

                // Read request body
                string requestBody;
                using (var reader = new StreamReader(HttpContext.Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                // Get Stripe signature from headers
                string stripeSignature = HttpContext.Request.Headers["Stripe-Signature"];

                bool result = await _paymentService.ProcessWebhookAsync(requestBody, stripeSignature, _config.StripeWebhookSecret);
                
                if (result)
                    return Ok();
                else
                    return BadRequest();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("cancel-subscription")]
        public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionModel model)
        {
            try
            {
                if (!_paymentService.IsPaymentEnabled)
                    return BadRequest(new { error = "Payment system is not configured." });

                // In a real app, get userId from authenticated user
                int userId = model.UserId;
                
                bool result = await _paymentService.CancelSubscriptionAsync(userId);
                
                if (result)
                    return Ok(new { message = "Subscription cancelled successfully." });
                else
                    return BadRequest(new { error = "Failed to cancel subscription." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions([FromQuery] int userId)
        {
            try
            {
                // In a real app, get userId from authenticated user
                var transactions = await _paymentService.GetUserTransactionsAsync(userId);
                
                return Ok(new { transactions });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("plans")]
        public IActionResult GetPlans()
        {
            try
            {
                var plans = new[]
                {
                    new {
                        id = "free",
                        name = "Free",
                        price = 0.00,
                        maxDomains = _config.FreeTierMaxDomains,
                        features = new[] {
                            $"Up to {_config.FreeTierMaxDomains} domains",
                            "Basic update frequency",
                            "Community support"
                        },
                        isEnabled = _config.FreeTierEnabled
                    },
                    new {
                        id = "basic",
                        name = "Basic",
                        price = _config.BasicPlanPrice,
                        maxDomains = _config.BasicPlanMaxDomains,
                        features = new[] {
                            $"Up to {_config.BasicPlanMaxDomains} domains",
                            "Faster update frequency",
                            "Email support",
                            "API access"
                        },
                        isEnabled = _paymentService.IsPaymentEnabled
                    },
                    new {
                        id = "pro",
                        name = "Pro",
                        price = _config.ProPlanPrice,
                        maxDomains = _config.ProPlanMaxDomains,
                        features = new[] {
                            $"Up to {_config.ProPlanMaxDomains} domains",
                            "Fastest update frequency",
                            "Priority support",
                            "Advanced API access",
                            "Custom domain settings"
                        },
                        isEnabled = _paymentService.IsPaymentEnabled
                    }
                };
                
                return Ok(new { plans });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class CheckoutModel
    {
        public int UserId { get; set; }
        public string Plan { get; set; }
        public string SuccessUrl { get; set; }
        public string CancelUrl { get; set; }
    }

    public class CancelSubscriptionModel
    {
        public int UserId { get; set; }
    }
}