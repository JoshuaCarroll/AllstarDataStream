namespace AsteriskAMIStream.Models
{
    public static class ConsoleHelper
    {
        public static void Write(string outputString, string prefix = "", ConsoleColor color = ConsoleColor.Gray, ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            ConsoleColor originalBackgroundColor = Console.BackgroundColor;

            Console.ForegroundColor = color;
            Console.BackgroundColor = backgroundColor;

            outputString = outputString.Replace("\n", "\n" + prefix);
            Console.WriteLine(outputString);

            Console.ForegroundColor = originalColor;
            Console.BackgroundColor = originalBackgroundColor;
        }
    }
}
