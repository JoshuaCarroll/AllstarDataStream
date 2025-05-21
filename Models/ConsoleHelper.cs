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

            var lines = outputString.Trim().Split('\n');
            foreach (var line in lines)
            {
                Console.WriteLine(prefix + line.Trim());
            }

            Console.ForegroundColor = originalColor;
            Console.BackgroundColor = originalBackgroundColor;
        }
    }
}
