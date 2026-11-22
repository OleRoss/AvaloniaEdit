using Avalonia.Headless.NUnit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using NUnit.Framework;

namespace AvaloniaEdit.Tests.Editing
{
    [TestFixture]
    internal class LongLineEditProtectionTests
    {
        private const int Limit = VisualLine.LENGTH_LIMIT;

        [AvaloniaTest]
        public void Line_At_The_Limit_Should_Accept_Text_At_Its_End()
        {
            var document = new TextDocument(new string('a', Limit));

            Assert.That(LongLineEditProtection.CanInsert(document, Limit), Is.True);
        }

        [AvaloniaTest]
        public void Longer_Line_Should_Refuse_Text_From_The_Limit_On()
        {
            var document = new TextDocument(new string('a', Limit + 1));

            Assert.That(LongLineEditProtection.CanInsert(document, Limit - 1), Is.True);
            Assert.That(LongLineEditProtection.CanInsert(document, Limit), Is.False);
        }
    }
}
