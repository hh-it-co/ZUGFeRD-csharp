﻿/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using Microsoft.VisualStudio.TestTools.UnitTesting;
using s2industries.ZUGFeRD;

namespace ZUGFeRD_Test
{
    [TestClass]
    public class XRechnungUBLTests : TestBase
    {
        InvoiceProvider InvoiceProvider = new InvoiceProvider();
        ZUGFeRDVersion version = ZUGFeRDVersion.Version23;


        [TestMethod]
        public void TestInvoiceCreation()
        {
            InvoiceDescriptor desc = this.InvoiceProvider.CreateInvoice();
            MemoryStream ms = new MemoryStream();

            desc.Save(ms, ZUGFeRDVersion.Version23, Profile.XRechnung, ZUGFeRDFormats.UBL);
            ms.Seek(0, SeekOrigin.Begin);

            InvoiceDescriptor loadedInvoice = InvoiceDescriptor.Load(ms);

            Assert.AreEqual(loadedInvoice.Invoicee, null);
            Assert.AreNotEqual(loadedInvoice.Seller, null);
            Assert.AreEqual(loadedInvoice.Taxes.Count, 2);
            Assert.AreEqual(loadedInvoice.SellerContact.Name, "Max Mustermann");
            Assert.IsNull(loadedInvoice.BuyerContact);
        } // !TestInvoiceCreation()


        [TestMethod]
        public void TestTradelineitemProductCharacterstics()
        {
            InvoiceDescriptor desc = this.InvoiceProvider.CreateInvoice();

            desc.TradeLineItems[0].ApplicableProductCharacteristics = new ApplicableProductCharacteristic[]
                    {
                        new ApplicableProductCharacteristic()
                        {
                            Description = "Test Description",
                            Value = "1.5 kg"
                        },
                        new ApplicableProductCharacteristic()
                        {
                            Description = "UBL Characterstics 2",
                            Value = "3 kg"
                        },
                    }.ToList();

            MemoryStream ms = new MemoryStream();

            desc.Save(ms, version, Profile.XRechnung, ZUGFeRDFormats.UBL);
            ms.Seek(0, SeekOrigin.Begin);

            InvoiceDescriptor loadedInvoice = InvoiceDescriptor.Load(ms);

            Assert.IsNotNull(loadedInvoice.TradeLineItems);
            Assert.AreEqual(loadedInvoice.TradeLineItems[0].ApplicableProductCharacteristics.Count, 2);
            Assert.AreEqual(loadedInvoice.TradeLineItems[0].ApplicableProductCharacteristics[0].Description, "Test Description");
            Assert.AreEqual(loadedInvoice.TradeLineItems[0].ApplicableProductCharacteristics[1].Value, "3 kg");
        } // !TestTradelineitemProductCharacterstics()


        /// <summary>
        /// https://github.com/stephanstapel/ZUGFeRD-csharp/issues/319
        /// </summary>
        [TestMethod]
        public void TestSkippingOfAllowanceChargeBasisAmount()
        {
            // actual values do not matter
            decimal basisAmount = 123.0m;
            decimal percent = 11.0m;
            decimal allowanceChargeBasisAmount = 121.0m;

            InvoiceDescriptor desc = this.InvoiceProvider.CreateInvoice();
            desc.AddApplicableTradeTax(basisAmount, percent, TaxTypes.LOC, TaxCategoryCodes.K, allowanceChargeBasisAmount);
            MemoryStream ms = new MemoryStream();

            desc.Save(ms, version, Profile.XRechnung, ZUGFeRDFormats.UBL);
            ms.Seek(0, SeekOrigin.Begin);

            InvoiceDescriptor loadedInvoice = InvoiceDescriptor.Load(ms);

            Tax tax = loadedInvoice.Taxes.FirstOrDefault(t => t.TypeCode == TaxTypes.LOC);
            Assert.IsNotNull(tax);
            Assert.AreEqual(basisAmount, tax.BasisAmount);
            Assert.AreEqual(percent, tax.Percent);
            Assert.AreEqual(null, tax.AllowanceChargeBasisAmount);
        } // !TestInvoiceCreation()

        [TestMethod]
        public void TestAllowanceChargeOnDocumentLevel()
        {
            InvoiceDescriptor desc = this.InvoiceProvider.CreateInvoice();

            // Test Values
            bool isDiscount = true;
            decimal? basisAmount = 123.45m;
            CurrencyCodes currency = CurrencyCodes.EUR;
            decimal actualAmount = 12.34m;
            string reason = "Gutschrift";
            TaxTypes taxTypeCode = TaxTypes.VAT;
            TaxCategoryCodes taxCategoryCode = TaxCategoryCodes.AA;
            decimal taxPercent = 19.0m;

            desc.AddTradeAllowanceCharge(isDiscount, basisAmount, currency, actualAmount, reason, taxTypeCode, taxCategoryCode, taxPercent);

            TradeAllowanceCharge? testAllowanceCharge = desc.GetTradeAllowanceCharges().FirstOrDefault();

            MemoryStream ms = new MemoryStream();

            desc.Save(ms, version, Profile.Extended, ZUGFeRDFormats.UBL);
            ms.Seek(0, SeekOrigin.Begin);

            InvoiceDescriptor loadedInvoice = InvoiceDescriptor.Load(ms);

            TradeAllowanceCharge loadedAllowanceCharge = loadedInvoice.GetTradeAllowanceCharges()[0];

            Assert.AreEqual(loadedInvoice.GetTradeAllowanceCharges().Count(), 1);
            Assert.AreEqual(loadedAllowanceCharge.ChargeIndicator, !isDiscount, message: "isDiscount");
            Assert.AreEqual(loadedAllowanceCharge.BasisAmount, basisAmount, message: "basisAmount");
            Assert.AreEqual(loadedAllowanceCharge.Currency, currency, message: "currency");
            Assert.AreEqual(loadedAllowanceCharge.Amount, actualAmount, message: "actualAmount");
            Assert.AreEqual(loadedAllowanceCharge.Reason, reason, message: "reason");
            Assert.AreEqual(loadedAllowanceCharge.Tax.TypeCode, taxTypeCode, message: "taxTypeCode");
            Assert.AreEqual(loadedAllowanceCharge.Tax.CategoryCode, taxCategoryCode, message: "taxCategoryCode");
            Assert.AreEqual(loadedAllowanceCharge.Tax.Percent, taxPercent, message: "taxPercent");

        } // !TestAllowanceChargeOnDocumentLevel

        [TestMethod]
        public void TestInvoiceWithAttachment()
        {
            InvoiceDescriptor desc = this.InvoiceProvider.CreateInvoice();
            string filename = "myrandomdata.bin";
            byte[] data = new byte[32768];
            new Random().NextBytes(data);

            desc.AddAdditionalReferencedDocument(
                id: "My-File",
                typeCode: AdditionalReferencedDocumentTypeCode.ReferenceDocument,
                name: "Ausführbare Datei",
                attachmentBinaryObject: data,
                filename: filename);

            MemoryStream ms = new MemoryStream();

            desc.Save(ms, version, Profile.Extended, ZUGFeRDFormats.UBL);
            ms.Seek(0, SeekOrigin.Begin);

            InvoiceDescriptor loadedInvoice = InvoiceDescriptor.Load(ms);

            Assert.AreEqual(loadedInvoice.AdditionalReferencedDocuments.Count, 1);

            foreach (AdditionalReferencedDocument document in loadedInvoice.AdditionalReferencedDocuments)
            {
                if (document.ID == "My-File")
                {
                    CollectionAssert.AreEqual(document.AttachmentBinaryObject, data);
                    Assert.AreEqual(document.Filename, filename);
                    break;
                }
            }
        } // !TestInvoiceWithAttachment()
    }
}
