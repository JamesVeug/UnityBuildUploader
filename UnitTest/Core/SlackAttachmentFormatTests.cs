using System.Collections.Generic;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    public class SlackAttachmentFormatTests
    {
        [Test]
        public void FormatResolvesTokensInTextFallbackAndActions()
        {
            Context context = new Context();
            context.AddCommand("{testVersion}", () => "1.2.3");

            SlackAttachment attachment = new SlackAttachment
            {
                text = "v{testVersion} uploaded",
                fallback = "{testVersion}",
                color = "#009000",
                actions = new List<SlackAttachment.Action>
                {
                    new SlackAttachment.Action { name = "open", text = "Open {testVersion}", type = "button" }
                }
            };

            SlackAttachment formatted = attachment.Format(context);

            Assert.AreEqual("v1.2.3 uploaded", formatted.text);
            Assert.AreEqual("1.2.3", formatted.fallback);
            Assert.AreEqual("#009000", formatted.color);
            Assert.AreEqual("Open 1.2.3", formatted.actions[0].text);
            Assert.AreEqual("open", formatted.actions[0].name);
        }

        [Test]
        public void FormatLeavesTheSavedAttachmentUnchanged()
        {
            Context context = new Context();
            context.AddCommand("{testVersion}", () => "1.2.3");

            SlackAttachment attachment = new SlackAttachment
            {
                text = "v{testVersion}",
                actions = new List<SlackAttachment.Action> { new SlackAttachment.Action { text = "{testVersion}" } }
            };

            attachment.Format(context);

            Assert.AreEqual("v{testVersion}", attachment.text);
            Assert.AreEqual("{testVersion}", attachment.actions[0].text);
        }
    }
}
