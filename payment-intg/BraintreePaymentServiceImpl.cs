using Braintree;
using Core.Data.Model.Entities;
using Core.Data.Model.Enums;
using Core.Logic.Services.Payments.Model.Braintree;
using Core.Logic.Services.Payments.Model.Braintree.Notification;
using Core.Logic.Services.Payments.Model.Braintree.Response;
using Core.Logic.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services.Payments.Impl
{
    public class BraintreePaymentServiceImpl : IBraintreePaymentService
    {
        private const String SERVICE_FEE_PERCENT = "BraintreeServiceFeePercent";
        private const String REFUND_DUPLICATE_ERR = "transaction has already been completely refunded";
        private const String NOT_IN_ECROW_ERR = "cannot release a transaction that is not escrowed";

        public BraintreePaymentServiceImpl(IConfigurationProvider configurationProvider)
        {
            _configurationProvider = configurationProvider;

            var environment = Braintree.Environment.SANDBOX;
            switch (_configurationProvider["BraintreeEnvironment"])
            {
                case "SANDBOX":
                    environment = Braintree.Environment.SANDBOX;
                    break;
                case "PRODUCTION":
                    environment = Braintree.Environment.PRODUCTION;
                    break;
            }

            _gateway = new BraintreeGateway
            {
                Environment = environment,
                MerchantId = _configurationProvider["BraintreeMerchantId"],
                PublicKey = _configurationProvider["BraintreeApiKey"],
                PrivateKey = _configurationProvider["BraintreeApiSecret"]
            };
        }

        #region public methods

        /// <summary>
        /// Gets the service fee amount from the specified amount
        /// </summary>
        /// <param name="amount">The amount</param>
        /// <returns></returns>
        public Decimal GetServiceFeeAmount(Decimal amount)
        {
            var serviceFeePercent = _configurationProvider.GetSetting<Decimal>(SERVICE_FEE_PERCENT);
            if (serviceFeePercent == 0) serviceFeePercent = 15;

            return amount * serviceFeePercent / 100;
        }

        /// <summary>
        /// Generates the client token
        /// </summary>
        /// <param name="customerId">The id of customer</param>
        /// <returns></returns>
        public GenerateClientTokenResponse GenerateClientToken(String customerId = null)
        {
            var request = new ClientTokenRequest
            {
                CustomerId = customerId,
                MerchantAccountId = _configurationProvider["BraintreeMerchantAccountId"],
                
            };

            if(!String.IsNullOrEmpty(customerId))
            {
                request.Options = new ClientTokenOptionsRequest
                {
                    VerifyCard = true,
                };
            }

            var result = _gateway.ClientToken.generate(request);

            var response = new GenerateClientTokenResponse
            {
                IsSuccess = !String.IsNullOrWhiteSpace(result),
            };

            if (response.IsSuccess)
            {
                response.ClientToken = result;
            }
            else
            {
                response.Errors = CollectResponseErrors(new ValidationErrors(), "Unable to generate client token");
            }

            return response;
        }

        /// <summary>
        /// Creates the sub-merchant account in Braintree
        /// </summary>
        /// <param name="individual">The individual merchant info</param>
        /// <param name="business">The business merchant info</param>
        /// <param name="funding">The funding merchant info</param>
        /// <param name="isTosAccepted">The TOS accepted flag</param>
        /// <param name="merchantAccountId">The existing merchant account Id</param>
        public CreateSubMerchantResponse CreateOrUpdateSubMerchant(IndividualMerchantInfo individual, BusinessMerchantInfo business, FundingMerchantInfo funding, 
                                                                   Boolean isTosAccepted, String merchantAccountId = null)
        {
            var request = new MerchantAccountRequest();
            request.Individual = new IndividualRequest
            {
                FirstName = individual.FirstName,
                LastName = individual.LastName,
                Email = individual.Email,
                Phone = individual.Phone,
                DateOfBirth = individual.DateOfBirth.ToString("yyyy-MM-dd"),
                Ssn = individual.Ssn,
                Address = new AddressRequest
                {
                    StreetAddress = individual.StreetAddress,
                    Locality = individual.Locality,
                    Region = individual.Region,
                    PostalCode = individual.PostalCode
                }
            };

            if (business != null)
            {
                request.Business = new BusinessRequest
                {
                    LegalName = business.LegalName,
                    DbaName = business.DbaName,
                    TaxId = business.TaxId,
                    Address = new AddressRequest
                    {
                        StreetAddress = business.StreetAddress,
                        Locality = business.Locality,
                        Region = business.Region,
                        PostalCode = business.PostalCode
                    }
                };
            }

            request.Funding = new FundingRequest
            {
                Descriptor = funding.Descriptor,
                Destination = FundingDestination.BANK, //TODO: check this logic
                Email = funding.Email,
                MobilePhone = funding.MobilePhone,
                AccountNumber = funding.AccountNumber,
                RoutingNumber = funding.RoutingNumber
            };

            request.TosAccepted = isTosAccepted;
            request.MasterMerchantAccountId = _configurationProvider["BraintreeMerchantAccountId"];

            var result = String.IsNullOrEmpty(merchantAccountId) ? _gateway.MerchantAccount.Create(request) :
                                                                   _gateway.MerchantAccount.Update(merchantAccountId, request);

            var response = new CreateSubMerchantResponse
            {
                IsSuccess = result.IsSuccess(),
            };

            if(response.IsSuccess)
            {
                response.SubMerchantId = result.Target.Id;
                response.AccountStatus = result.Target.Status.ToString();
            }
            else
            {
                response.Errors = CollectResponseErrors(result.Errors, result.Message);
            }

            return response;
        }

        /// <summary>
        /// Parses the web-hook notification
        /// </summary>
        /// <param name="signature">The notification signature</param>
        /// <param name="payload">The payload</param>
        /// <returns></returns>
        public WebHookNotificationModel ParseWebHookNotification(String signature, String payload)
        {
            var notification = _gateway.WebhookNotification.Parse(signature, payload);
            var result = new WebHookNotificationModel();

            if(notification.Kind == WebhookKind.SUB_MERCHANT_ACCOUNT_APPROVED ||
               notification.Kind == WebhookKind.SUB_MERCHANT_ACCOUNT_DECLINED)
            {
                result.Kind = notification.Kind == WebhookKind.SUB_MERCHANT_ACCOUNT_APPROVED ? BraintreeWebHookKind.SubMerchantApproved 
                                                                                             : BraintreeWebHookKind.SubMerchantDeclined;
                result.SubMerchantAccountId = notification.MerchantAccount.Id;
                result.SubMerchantAccountStatus = notification.MerchantAccount.Status.ToString().ConvertTo<SubMerchantAccountStatus>();
            }

            return result;
        }

        /// <summary>
        /// Creates the customer in Braintree system
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="customer">The customenr info</param>
        /// <returns></returns>
        public CreateCustomerResponse CreateCustomer(CustomerInfo customer)
        {
            var request = new CustomerRequest
            {
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Phone = customer.Phone,
                Company = customer.Company,
                Website = customer.Website,
            };

            var result = _gateway.Customer.Create(request);

            var response = new CreateCustomerResponse
            {
                IsSuccess = result.IsSuccess(),
            };

            if (response.IsSuccess)
            {
                response.CustomerId = result.Target.Id;
            }
            else
            {
                response.Errors = CollectResponseErrors(result.Errors, result.Message);
            }

            return response;
        }

        /// <summary>
        /// Confirms the customer via checking the entered credit card
        /// </summary>
        /// <param name="customerId">The id of customer</param>
        /// <param name="paymentMethodNonce">The payment method nonce</param>
        /// <returns></returns>
        public ConfirmCustomerResponse ConfirmCustomer(String customerId, String paymentMethodNonce)
        {
            var request = new CustomerRequest
            {
                CreditCard = new CreditCardRequest
                {
                    PaymentMethodNonce = paymentMethodNonce,
                    Options = new CreditCardOptionsRequest
                    {
                        VerificationAmount = "0.05",
                        VerifyCard = true,
                        VerificationMerchantAccountId = _configurationProvider["BraintreeMerchantAccountId"],
                    }
                }
            };

            var result = _gateway.Customer.Update(customerId, request);

            var response = new ConfirmCustomerResponse
            {
                IsSuccess = result.IsSuccess(),
            };

            if (response.IsSuccess)
            {
                response.CreditCartToken = result.Target.CreditCards.Any() ? result.Target.CreditCards[0].Token : null;
            }
            else
            {
                response.Errors = CollectResponseErrors(result.Errors, result.Message);
            }

            return response;
        }

        /// <summary>
        /// Gets the sub-merchant account status
        /// </summary>
        /// <param name="subMerchantId">The sub-merchant account id</param>
        /// <returns></returns>
        public String GetSubMerchantStatus(String subMerchantId)
        {
            var merchantAccount = _gateway.MerchantAccount.Find(subMerchantId);

            return merchantAccount != null ? merchantAccount.Status.ToString() : null;
        }

        /// <summary>
        /// Gets the sub-merchant account info
        /// </summary>
        /// <param name="subMerchantId">The sub-merchant account id</param>
        /// <returns></returns>
        public Tuple<IndividualMerchantInfo, FundingMerchantInfo, BusinessMerchantInfo> GetSubMerchantInfo(String subMerchantId)
        {
            var merchantAccount = _gateway.MerchantAccount.Find(subMerchantId);
            if(merchantAccount != null)
            {
                IndividualMerchantInfo individual = null;
                if (merchantAccount.IndividualDetails != null)
                {
                    individual = new IndividualMerchantInfo
                    {
                        DateOfBirth = DateTime.Parse(merchantAccount.IndividualDetails.DateOfBirth),
                        Email = merchantAccount.IndividualDetails.Email,
                        FirstName = merchantAccount.IndividualDetails.FirstName,
                        LastName = merchantAccount.IndividualDetails.LastName,
                        Locality = merchantAccount.IndividualDetails.Address?.Locality,
                        Phone = merchantAccount.IndividualDetails.Phone,
                        PostalCode = merchantAccount.IndividualDetails.Address?.PostalCode,
                        Region = merchantAccount.IndividualDetails.Address?.Region,
                        Ssn = merchantAccount.IndividualDetails.SsnLastFour,
                        StreetAddress = merchantAccount.IndividualDetails.Address?.StreetAddress
                    };
                }

                FundingMerchantInfo funding = null;
                if (merchantAccount.FundingDetails != null)
                {
                    funding = new FundingMerchantInfo
                    {
                        AccountNumber = merchantAccount.FundingDetails.AccountNumberLast4,
                        Descriptor = merchantAccount.FundingDetails.Descriptor,
                        Email = merchantAccount.FundingDetails.Email,
                        FundingDestination = FundingDestination.BANK.ToString(),
                        MobilePhone = merchantAccount.FundingDetails.MobilePhone,
                        RoutingNumber = merchantAccount.FundingDetails.RoutingNumber
                    };
                }

                BusinessMerchantInfo businnes = null;
                if(merchantAccount.BusinessDetails != null)
                {
                    businnes = new BusinessMerchantInfo
                    {
                        DbaName = merchantAccount.BusinessDetails.DbaName,
                        LegalName = merchantAccount.BusinessDetails.LegalName,
                        Locality = merchantAccount.BusinessDetails.Address?.Locality,
                        PostalCode = merchantAccount.BusinessDetails.Address?.PostalCode,
                        Region = merchantAccount.BusinessDetails.Address?.Region,
                        StreetAddress = merchantAccount.BusinessDetails.Address?.StreetAddress,
                        TaxId = merchantAccount.BusinessDetails.TaxId
                    };
                }

                return new Tuple<IndividualMerchantInfo, FundingMerchantInfo, BusinessMerchantInfo>(individual, funding, businnes);
            }

            return null;
        }

        /// <summary>
        /// Performs the payment for the reserved spot
        /// </summary>
        /// <param name="spot">The reserved spot</param>
        /// <param name="paymentMethodNonce">The payment method nonce</param>
        /// <param name="customerId">The Id of customer</param>
        /// <returns></returns>
        public SpotPaymentResponse PerformSpotPayment(Spot spot, String paymentMethodNonce, String customerId = null)
        {
            var holdInEscrow = _configurationProvider.GetSetting<Boolean>("BraintreeHoldInEscrow");
            var feeAmount = GetServiceFeeAmount(spot.TotalCost);

            var request = new TransactionRequest
            {
                Amount = spot.TotalCost,
                PaymentMethodNonce = paymentMethodNonce,
                CustomerId = customerId,
                OrderId = Guid.NewGuid().OnlySymbols(),
                MerchantAccountId = spot.Event.Host.SubMerchant.SubMerchantAccountId,
                ServiceFeeAmount = feeAmount,

                Options = new TransactionOptionsRequest
                {
                    SubmitForSettlement = true,
                    StoreInVaultOnSuccess = true,
                    HoldInEscrow = holdInEscrow
                },
            };

            var result = _gateway.Transaction.Sale(request);

            var response = new SpotPaymentResponse
            {
                IsSuccess = result.IsSuccess(),
                IsHoldInEscrow = holdInEscrow,
                FeeAmount = feeAmount
            };

            if (response.IsSuccess)
            {
                response.TransactionId = result.Target.Id;
                response.TransactionStatus = result.Target.Status.ToString();
                response.OrderId = result.Target.OrderId;
            }
            else
            {
                response.Errors = CollectResponseErrors(result.Errors, result.Message);
            }

            return response;
        }

        /// <summary>
        /// Releases the spot payment from escrow
        /// </summary>
        /// <param name="spot">The spot to release</param>
        /// <returns></returns>
        public ReleaseFromEscrowResponse ReleasePaymentFromEscrow(Spot spot)
        {
            var result = _gateway.Transaction.ReleaseFromEscrow(spot.SpotPayment.TransactionId);

            var response = new ReleaseFromEscrowResponse
            {
                IsSuccess = result.IsSuccess(),
            };

            if (response.IsSuccess)
            {
                response.TransactionStatus = result.Target.Status.ToString();
            }
            else
            {
                response.Errors = CollectResponseErrors(result.Errors, result.Message);
                //if (response.Errors.Any(x => x.Value.ToLower().Contains(NOT_IN_ECROW_ERR)))
                //{
                //    response.IsSuccess = true;
                //    response.TransactionStatus = response.Errors.FirstOrDefault(x => x.Value.ToLower().Contains(NOT_IN_ECROW_ERR)).Value;
                //}
            }

            return response;
        }

        /// <summary>
        /// Refund the spefified spot payment
        /// </summary>
        /// <param name="spot">The spot with payment</param>
        /// <returns></returns>
        public RefundOrVoidResponse RefundSpotPayment(Spot spot)
        {
            var result = _gateway.Transaction.Refund(spot.SpotPayment.TransactionId, new TransactionRefundRequest
            {
                OrderId = spot.SpotPayment.OrderId
            });

            var response = new RefundOrVoidResponse
            {
                IsSuccess = result.IsSuccess(),
            };

            if (response.IsSuccess)
            {
                response.TransactionId = result.Target.Id;
                response.TransactionStatus = result.Target.Status.ToString();
            }
            else
            {
                response.Errors = CollectResponseErrors(result.Errors, result.Message);
                if (response.Errors.Any(x => x.Value.ToLower().Contains(REFUND_DUPLICATE_ERR)))
                {
                    response.IsSuccess = true;
                    response.TransactionStatus = response.Errors.FirstOrDefault(x => x.Value.ToLower().Contains(REFUND_DUPLICATE_ERR)).Value;
                }
            }

            return response;
        }

        /// <summary>
        /// Refund the spefified spot payment
        /// </summary>
        /// <param name="spot">The spot with payment</param>
        /// <returns></returns>
        public RefundOrVoidResponse VoidSpotPayment(Spot spot)
        {
            var result = _gateway.Transaction.Void(spot.SpotPayment.TransactionId);

            var response = new RefundOrVoidResponse
            {
                IsSuccess = result.IsSuccess(),
            };

            if (response.IsSuccess)
            {
                response.TransactionId = result.Target.Id;
                response.TransactionStatus = result.Target.Status.ToString();
            }
            else
            {
                response.Errors = CollectResponseErrors(result.Errors, result.Message);
                if (response.Errors.Any(x => x.Value.ToLower().Contains(REFUND_DUPLICATE_ERR)))
                {
                    response.IsSuccess = true;
                    response.TransactionStatus = response.Errors.FirstOrDefault(x => x.Value.ToLower().Contains(REFUND_DUPLICATE_ERR)).Value;
                }
            }

            return response;
        }

        /// <summary>
        /// Voids or refunds the spot payment depending on its transaction state
        /// </summary>
        /// <param name="spot">The spot</param>
        /// <returns></returns>
        public RefundOrVoidResponse VoidOrRefundSpotPayment(Spot spot)
        {
            var transaction = _gateway.Transaction.Find(spot.SpotPayment.TransactionId);
            if(transaction != null)
            {
                if(transaction.Status == TransactionStatus.SETTLED)
                {
                    return RefundSpotPayment(spot);
                }
                else
                {
                    return VoidSpotPayment(spot);
                }
            }
            else
            {
                return new RefundOrVoidResponse
                {
                    Errors = CollectResponseErrors(null, "Transaction not found")
                };
            }
        }

        /// <summary>
        /// Gets the spot payment transaction info
        /// </summary>
        /// <param name="spot">The spot</param>
        /// <returns></returns>
        public PaymentInfoResponse RetrievePaymentInfo(Spot spot)
        {
            var transaction = _gateway.Transaction.Find(spot.SpotPayment.TransactionId);
            if (transaction != null)
            {
                return new PaymentInfoResponse
                {
                    IsSuccess = true,
                    TransactionId = transaction.Id,
                    OrderId = transaction.OrderId,
                    TransactionStatus = transaction.Status.ToString(),
                    IsHoldInEscrow = transaction.EscrowStatus == TransactionEscrowStatus.HELD || 
                                     transaction.EscrowStatus == TransactionEscrowStatus.HOLD_PENDING
                };
            }

            return null;
        }

        #endregion public methods

        #region private methods

        private Dictionary<String, String> CollectResponseErrors(ValidationErrors errors, String message)
        {
            var result = new Dictionary<String, String>();

            if (errors != null && errors.Count > 0)
            {
                foreach (var error in errors.All())
                {
                    if (!result.ContainsKey(error.Attribute))
                    {
                        result[error.Attribute] = $"{error.Code} | {error.Message}";
                    }
                }
            }

            if (!String.IsNullOrWhiteSpace(message))
            {
                result.Add("_global", message);
            }

            return result;
        }

        #endregion private methods

        #region private fields

        private readonly BraintreeGateway _gateway;
        private readonly IConfigurationProvider _configurationProvider;

        #endregion private fields
    }
}
