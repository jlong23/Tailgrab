namespace Tailgrab.Common
{
    public enum AnsiColor
    {
        Reset,
        Black,
        Red,
        Green,
        Yellow,
        Blue,
        Magenta,
        Cyan,
        White,
        BrightBlack,
        BrightRed,
        BrightGreen,
        BrightYellow,
        BrightBlue,
        BrightMagenta,
        BrightCyan,
        BrightWhite
    }

    public static class AnsiColorExtensions
    {
        public static int GetAnsiCode(this AnsiColor color)
        {
            return color switch
            {
                AnsiColor.Reset => 0,
                AnsiColor.Black => 30,
                AnsiColor.Red => 31,
                AnsiColor.Green => 32,
                AnsiColor.Yellow => 33,
                AnsiColor.Blue => 34,
                AnsiColor.Magenta => 35,
                AnsiColor.Cyan => 36,
                AnsiColor.White => 37,
                AnsiColor.BrightBlack => 90,
                AnsiColor.BrightRed => 91,
                AnsiColor.BrightGreen => 92,
                AnsiColor.BrightYellow => 93,
                AnsiColor.BrightBlue => 94,
                AnsiColor.BrightMagenta => 95,
                AnsiColor.BrightCyan => 96,
                AnsiColor.BrightWhite => 97,
                _ => 0,
            };
        }

        public static string GetAnsiEscape(this AnsiColor color)
        {
            var code = color.GetAnsiCode();
            return $"\u001b[{code}m";
        }

        public static string GetDescription(this AnsiColor color)
        {
            return color switch
            {
                AnsiColor.Reset => "Reset",
                AnsiColor.Black => "Black",
                AnsiColor.Red => "Red",
                AnsiColor.Green => "Green",
                AnsiColor.Yellow => "Yellow",
                AnsiColor.Blue => "Blue",
                AnsiColor.Magenta => "Magenta",
                AnsiColor.Cyan => "Cyan",
                AnsiColor.White => "White",
                AnsiColor.BrightBlack => "Bright Black / Gray",
                AnsiColor.BrightRed => "Bright Red",
                AnsiColor.BrightGreen => "Bright Green",
                AnsiColor.BrightYellow => "Bright Yellow",
                AnsiColor.BrightBlue => "Bright Blue",
                AnsiColor.BrightMagenta => "Bright Magenta",
                AnsiColor.BrightCyan => "Bright Cyan",
                AnsiColor.BrightWhite => "Bright White",
                _ => "Unknown",
            };
        }
    }
}
