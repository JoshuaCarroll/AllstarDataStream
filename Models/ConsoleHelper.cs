namespace AsteriskDataStream.Models
{
    public static class ConsoleHelper
    {
        public static void Write(string outputString, string prefix = "", ConsoleColor color = ConsoleColor.Gray, ConsoleColor backgroundColor = ConsoleColor.Black, bool includeCrLf = true)
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
    }
}
