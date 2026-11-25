using System;
using System.Globalization;

namespace ArmOrb
{
    // A bounded integer draft. The owning setting changes only after confirmation.
    public sealed class NumberEntry
    {
        readonly long minimum,maximum;
        readonly int maxDigits;
        bool replace=true;
        public string Text { get; private set; }
        public long Value => Math.Max(minimum,Math.Min(maximum,long.TryParse(Text,NumberStyles.None,CultureInfo.InvariantCulture,out long n)?n:0));
        public NumberEntry(long current,long minimum,long maximum)
        {
            if(minimum<0||maximum<minimum)throw new ArgumentOutOfRangeException(nameof(maximum));
            this.minimum=minimum;this.maximum=maximum;maxDigits=maximum.ToString(CultureInfo.InvariantCulture).Length;
            Text=Math.Max(minimum,Math.Min(maximum,current)).ToString(CultureInfo.InvariantCulture);
        }
        public void Digit(int digit)
        {
            if(digit<0||digit>9)return;
            string next=replace?"":Text;replace=false;
            if(next=="0")next="";
            if(next.Length<maxDigits)Text=next+digit.ToString(CultureInfo.InvariantCulture);
        }
        public void Backspace(){if(replace){Text="";replace=false;}else if(Text.Length>0)Text=Text.Substring(0,Text.Length-1);}
        public void Clear(){Text="";replace=false;}
    }
}
