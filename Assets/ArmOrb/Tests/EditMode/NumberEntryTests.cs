using NUnit.Framework;
namespace ArmOrb.Tests
{
    public sealed class NumberEntryTests
    {
        [Test] public void FirstDigitReplacesOldValueAndBackspaceEditsOnlyTheDraft()
        {var n=new NumberEntry(300,1,99999);n.Digit(4);n.Digit(5);Assert.That(n.Text,Is.EqualTo("45"));n.Backspace();Assert.That(n.Value,Is.EqualTo(4));}
        [Test] public void EmptyAndOutOfRangeHealthClampAtConfirmation()
        {var n=new NumberEntry(100,1,99999);n.Clear();Assert.That(n.Value,Is.EqualTo(1));for(int i=0;i<8;i++)n.Digit(9);Assert.That(n.Text,Is.EqualTo("99999"));Assert.That(n.Value,Is.EqualTo(99999));}
        [Test] public void StakeSupportsLargeValuesWithoutOverflowAndKeepsZero()
        {var n=new NumberEntry(100,0,1000000000000L);n.Clear();n.Digit(0);Assert.That(n.Text,Is.EqualTo("0"));for(int i=0;i<20;i++)n.Digit(9);Assert.That(n.Value,Is.EqualTo(1000000000000L));}
    }
}
