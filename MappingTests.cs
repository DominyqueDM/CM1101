using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Pwinty.Services.OneFlowShipping.Extensions;
using Pwinty.Services.OneFlowShipping.Model;
using Pwinty.Services.OneFlowShipping.Models;
using Pwinty.Services.OneFlowShipping.Providers.DHLde.Mapping;
using Pwinty.Services.OneFlowShipping.Providers.TestingShared;
using ServiceReference;

namespace Pwinty.Services.OneFlowShipping.Providers.DHLde.Tests
{
    [TestFixture]
    public class MappingTests
    {
        [SetUp]
        public void SetupTests()
        {
        }

        [Test]
        public async Task MappingValuesTest()
        {
            var testJson = await File.ReadAllTextAsync("OneFlowExample.Fr.json");
            
            var example = JsonConvert.DeserializeObject<ShipmentRequest>(testJson);

            var mapper = new ShipmentOrderMapper();

            var mappedOrder = mapper.Map(example, new ConsoleLogger(), "poop", null);

            mappedOrder.sequenceNumber.Should().Be("3334430");
            mappedOrder.Shipment.ShipmentDetails.product.Should().Be("V53WPAK");
            mappedOrder.Shipment.ShipmentDetails.accountNumber.Should().Be("22222222225301");

            (mappedOrder.Shipment.Item as ShipperType).Name.name1.Should().Be("Prodigi");
            (mappedOrder.Shipment.Item as ShipperType).Address.zip.Should().Be("GU10 2DZ");
        }

        [Test]
        public async Task Shipper_address_override_maps()
        {
            var testJson = await File.ReadAllTextAsync("OneFlowExample.json");
            var example = JsonConvert.DeserializeObject<ShipmentRequest>(testJson);

            var mapper = new ShipmentOrderMapper();
            
            var mappedOrder = mapper.Map(example, new ConsoleLogger(), "poop",  new ShipperAddressOverride()
            {
                ContactPerson = "Reginald Perrin",
                City = "Climthorpe",
                StreetNumber = "12",
                StreetName  = "Coleridge Close",
                Zip = "123456",
                Email = "email@prodigi.com",
                Name = "Rise and Fall",
                Phone = "123456",
                OriginCountry = "ZZ"
            });

            var shipper = (mappedOrder.Shipment.Item as ShipperType);
            
            shipper.Name.name1.Should().Be("Rise and Fall");
            shipper.Communication.contactPerson.Should().Be("Reginald Perrin");
            shipper.Communication.email.Should().Be("email@prodigi.com");
            shipper.Communication.phone.Should().Be("123456");
            shipper.Address.city.Should().Be("Climthorpe");
            shipper.Address.province.Should().Be(null);
            shipper.Address.streetName.Should().Be("Coleridge Close");
            shipper.Address.streetNumber.Should().Be("12");
            shipper.Address.zip.Should().Be("123456");

        }
        
        [Test]
        [TestCase("Just a normal string", ".", "Just a normal string")]
        [TestCase("27 High street", ".", "27 High street")]
        [TestCase("27 High street", ".", "27 High street")]
        [TestCase("27 High street 27", "27", "27 High street")]
        [TestCase("27 High street 27-88", "27-88", "27 High street")]
        [TestCase("27 High street 27a", "27a", "27 High street")]
        [TestCase("27 High 35 street", "35 street", "27 High")]
        public void DE_street_number_are_seperated_as_expected(string testValue, string expectedStreetNumber, string expectedStreetName)
        {
            var numb  = testValue.StreetNumber();
            
            testValue.StreetName().Should().Be(expectedStreetName);
            testValue.StreetNumber().Should().Be(expectedStreetNumber);
        }


        [Test]
        [TestCase("12345", "12345")]
        [TestCase("12345                ", "12345")]
        [TestCase("        12345", "12345")]
        [TestCase("        12345                ", "12345")]
        [TestCase(" 12345 ", "12345")]
        [TestCase("12345 ", "12345")]
        [TestCase(" 12345","12345")]
        public void address_elements_that_have_leading_and_trailing_white_space_should_be_trimed(string testZip, string expectedZip)
        {
            var example = new ShipmentRequest();

            example.ShipTo = new Address()
            {
                Address1 = "",
                Address2 = "",
                Email = "test@prodigi.com",
                Name = "Someone being tested",
                Phone = "",
                Postcode = testZip,
                State = "",
                Town = "",
                IsoCountry = "DE"
            };

            example.Shipper = new Shipper();

            example.Packages = new System.Collections.Generic.List<Package>();

            example.Packages.Add(new Package());

            example.Carrier = new Carrier()
            {
                Extra = "{\"product\":\"V01PAK\", \"accounatNumber\":\"22222222220101\"}"
            };

            var mapper = new ShipmentOrderMapper();

            var mappedOrder = mapper.Map(example, new ConsoleLogger(), "poop", null);

            var address = mappedOrder.Shipment.Receiver.Item as ReceiverNativeAddressType;

            address.zip.Should().Be(expectedZip);
        }




        [Test]
        [TestCase("LabelResponseExample.DHLPacket.xml", false, false)]
        [TestCase("LabelResponseExample.DHLPacketInternational.xml", true, false)]
        [TestCase("LabelResponseExample.DHLPacketInternational.GoGreenFalse.xml", true, true)]
        public void Check_response_deserialize_values(string source, bool includesServices, bool checkGoGreen)
        {
            // Create an instance of the XmlSerializer.
            XmlSerializer serializer =
                new XmlSerializer(typeof(ShipmentData));

            ShipmentData data;
            using (Stream reader = new FileStream(source, FileMode.Open))
            {
                // Call the Deserialize method to restore the object's state.
                data = (ShipmentData) serializer.Deserialize(reader);
            }

            data.ShipmentDetails.product.Should().NotBeNull();
            data.ShipmentDetails.accountNumber.Should().NotBeNull();
            data.ShipmentDetails.RoutingBarcode.Should().NotBeNull();
            data.ShipmentDetails.ShipmentItem.Should().NotBeNull();
            data.ShipmentDetails.Identifier.Should().NotBeNull();

            if (includesServices)
            {
                if (checkGoGreen)
                {
                    data.ShipmentDetails.Service.GoGreen.active.Should().BeFalse();
                }
                else
                {
                    data.ShipmentDetails.Service.Premium.active.Should().BeTrue();
                }
            }
            else
            {
                data.ShipmentDetails.Service.Should().BeNull();
            }
        }
    }
}