﻿namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceControl.Contracts;

    [TestFixture]
    class When_a_message_has_failed_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ctx.ExternalProcessorSubscribed = true;
                }
            });

            var context = await Define<MyContext>()
                .WithEndpoint<FailingReceiver>(b => b.When(c => c.ExternalProcessorSubscribed, bus => bus.SendLocal(new MyMessage {Body = "Faulty message"})).DoNotFailOnErrorMessages())
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<MessageFailed>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.EventDelivered)
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<MessageFailed>(context.Event);

            Assert.AreEqual("Faulty message", deserializedEvent.FailureDetails.Exception.Message);
            //These are important so check it they are set
            Assert.IsNotNull(deserializedEvent.MessageDetails.MessageId);
            Assert.IsNotNull(deserializedEvent.SendingEndpoint.Name);
            Assert.IsNotNull(deserializedEvent.ProcessingEndpoint.Name);
        }

        public class FailingReceiver : EndpointConfigurationBuilder
        {
            public FailingReceiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => { c.Recoverability().Immediate(s => s.NumberOfRetries(2)).Delayed(s => s.NumberOfRetries(0)); });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    throw new Exception(message.Body);
                }
            }
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JsonServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<MessageFailed>(Settings.DEFAULT_SERVICE_NAME); });
            }

            public class FailureHandler : IHandleMessages<MessageFailed>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageFailed message, IMessageHandlerContext context)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message);
                    Context.Event = serializedMessage;
                    Context.EventDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }


        public class MyMessage : ICommand
        {
            public string Body { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool ExternalProcessorSubscribed { get; set; }
            public string Event { get; set; }
            public bool EventDelivered { get; set; }
        }
    }
}