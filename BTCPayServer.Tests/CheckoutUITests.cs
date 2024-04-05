using System;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Payments;
using BTCPayServer.Tests.Logging;
using BTCPayServer.Views.Stores;
using NBitcoin;
using OpenQA.Selenium;
using Xunit;
using Xunit.Abstractions;

namespace BTCPayServer.Tests
{
    [Trait("Selenium", "Selenium")]
    [Collection(nameof(NonParallelizableCollectionDefinition))]
    public class CheckoutUITests : UnitTestBase
    {
        public const int TestTimeout = TestUtils.TestTimeout;
        public CheckoutUITests(ITestOutputHelper helper) : base(helper)
        {
        }

        [Fact(Timeout = TestTimeout)]
        public async Task CanUseLanguageDropdown()
        {
            using var s = CreateSeleniumTester();
            await s.StartAsync();
            s.GoToRegister();
            s.RegisterNewUser();
            s.CreateNewStore();
            s.AddDerivationScheme();

            var invoiceId = s.CreateInvoice();
            s.GoToInvoiceCheckout(invoiceId);
            Assert.True(s.Driver.FindElement(By.Id("DefaultLang")).FindElements(By.TagName("option")).Count > 1);
            var payWithTextEnglish = s.Driver.FindElement(By.Id("pay-with-text")).Text;

            var prettyDropdown = s.Driver.FindElement(By.Id("prettydropdown-DefaultLang"));
            prettyDropdown.Click();
            await Task.Delay(200);
            prettyDropdown.FindElement(By.CssSelector("[data-value=\"da-DK\"]")).Click();
            await Task.Delay(1000);
            Assert.NotEqual(payWithTextEnglish, s.Driver.FindElement(By.Id("pay-with-text")).Text);
            s.Driver.Navigate().GoToUrl(s.Driver.Url + "?lang=da-DK");

            Assert.NotEqual(payWithTextEnglish, s.Driver.FindElement(By.Id("pay-with-text")).Text);

            s.Driver.Quit();
        }

        [Fact(Timeout = TestTimeout)]
        [Trait("Lightning", "Lightning")]
        public async Task CanSetDefaultPaymentMethod()
        {
            using var s = CreateSeleniumTester();
            s.Server.ActivateLightning();
            await s.StartAsync();
            s.GoToRegister();
            s.RegisterNewUser(true);
            s.CreateNewStore();
            s.AddLightningNode();
            s.AddDerivationScheme();

            var invoiceId = s.CreateInvoice(defaultPaymentMethod: "BTC-LN");
            s.GoToInvoiceCheckout(invoiceId);
            Assert.Equal("Lightning", s.Driver.FindElement(By.ClassName("payment__currencies")).Text);
            s.Driver.Quit();
        }

        [Fact(Timeout = TestTimeout)]
        [Trait("Lightning", "Lightning")]
        public async Task CanUseLightningSatsFeature()
        {
            using var s = CreateSeleniumTester();
            s.Server.ActivateLightning();
            await s.StartAsync();
            s.GoToRegister();
            s.RegisterNewUser(true);
            s.CreateNewStore();
            s.AddLightningNode();
            s.GoToLightningSettings();
            s.Driver.SetCheckbox(By.Id("LightningAmountInSatoshi"), true);
            s.Driver.FindElement(By.Id("save")).Click();
            Assert.Contains("BTC Lightning settings successfully updated", s.FindAlertMessage().Text);

            var invoiceId = s.CreateInvoice(10, "USD", "a@g.com");
            s.GoToInvoiceCheckout(invoiceId);
            Assert.Contains("sats", s.Driver.FindElement(By.ClassName("buyerTotalLine")).Text);
        }

        [Fact(Timeout = TestTimeout)]
        public async Task CanUseJSModal()
        {
            using var s = CreateSeleniumTester();
            await s.StartAsync();
            s.GoToRegister();
            s.RegisterNewUser();
            s.CreateNewStore();
            s.GoToStore();
            s.AddDerivationScheme();
            var invoiceId = s.CreateInvoice(0.001m, "BTC", "a@x.com");
            var invoice = await s.Server.PayTester.InvoiceRepository.GetInvoice(invoiceId);
            s.Driver.Navigate()
                .GoToUrl(new Uri(s.ServerUri, $"tests/index.html?invoice={invoiceId}"));
            TestUtils.Eventually(() =>
            {
                Assert.True(s.Driver.FindElement(By.Name("btcpay")).Displayed);
            });
            await s.Server.ExplorerNode.SendToAddressAsync(BitcoinAddress.Create(invoice
                    .GetPaymentPrompt(PaymentTypes.CHAIN.GetPaymentMethodId("BTC"))
                    .Destination, Network.RegTest),
                new Money(0.001m, MoneyUnit.BTC));

            IWebElement closebutton = null;
            TestUtils.Eventually(() =>
            {
                var frameElement = s.Driver.FindElement(By.Name("btcpay"));
                var iframe = s.Driver.SwitchTo().Frame(frameElement);
                closebutton = iframe.FindElement(By.ClassName("close-action"));
                Assert.True(closebutton.Displayed);
            });
            closebutton.Click();
            s.Driver.AssertElementNotFound(By.Name("btcpay"));
            Assert.Equal(s.Driver.Url,
                new Uri(s.ServerUri, $"tests/index.html?invoice={invoiceId}").ToString());
        }
    }
}
