using System.Text;

namespace AsteriskDataStream.Models
{
    public static class ConsoleHelper
    {
        private static ConsoleColor _originalForeColor = ConsoleColor.Green;
        private static ConsoleColor _originalBackColor = ConsoleColor.Black;


        static ConsoleHelper()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.CursorVisible = false; // Hide the cursor for cleaner output
            Console.Title = "Asterisk Data Stream"; // Set the console title

            _originalForeColor = Console.ForegroundColor;
            _originalBackColor = Console.BackgroundColor;
        }

        public static void Write(string outputString)
        {
            Write(outputString, "", _originalForeColor, _originalBackColor, false);
        }

        public static void Write(string outputString, ConsoleColor foreColor)
        {
            Write(outputString, "", foreColor, _originalBackColor, false);
        }

        public static void Write(string outputString, string prefix, ConsoleColor color, ConsoleColor backgroundColor, bool includeCrLf)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            ConsoleColor originalBackgroundColor = Console.BackgroundColor;

            Console.ForegroundColor = color;
            Console.BackgroundColor = backgroundColor;

            outputString = outputString.Replace("\n", "\n" + prefix);

            if (includeCrLf)
            {
                Console.Write($"\r\n{outputString}  ");
            } else {
                Console.Write(outputString);
            }

            Console.ForegroundColor = originalColor;
            Console.BackgroundColor = originalBackgroundColor;
        }

        public static void WriteLine(string outputString)
        {
            Write(outputString, "", _originalForeColor, _originalBackColor, true);
        }

        public static void WriteLine(string outputString, ConsoleColor foreColor)
        {
            Write(outputString, "", foreColor, _originalBackColor, true);
        }

        public static void Rewrite(string outputString, int numberOfCharactersToRewrite, ConsoleColor foreColor)
        {
            // Move cursor back to overwrite the last N characters
            Console.SetCursorPosition(Console.CursorLeft - numberOfCharactersToRewrite, Console.CursorTop);
            Write(outputString, "", foreColor, _originalBackColor, false);
        }
    }
}
