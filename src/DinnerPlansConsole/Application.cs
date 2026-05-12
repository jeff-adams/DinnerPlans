using Microsoft.Extensions.Configuration;
using Spectre.Console;

public class Application(IConfiguration configuration)
{
    private readonly IConfiguration config = configuration;

    public void Run()
    {
        // DrawTest();

        while (true)
        {
            AnsiConsole.Clear();

            DrawHeader();
            DrawMainMenu().Action();
        }
    }

    private void DrawTest()
    {
        AnsiConsole.Clear();

        // Create main grid
        var mainGrid = new Grid { Expand = true };
        mainGrid.AddColumn();

        // Create header
        var header = new Panel("[bold yellow]System Dashboard[/]")
            .BorderColor(Color.Yellow)
            .RoundedBorder()
            .Expand();

        mainGrid.AddRow(header);
        mainGrid.AddEmptyRow();

        // Create metrics grid
        var metricsGrid = new Grid();
        metricsGrid.AddColumns(3);

        var cpuPanel = new Panel("[green]CPU: 45%[/]")
            .Header("Processor")
            .BorderColor(Color.Green);

        var memPanel = new Panel("[yellow]Memory: 8.2GB[/]")
            .Header("RAM")
            .BorderColor(Color.Yellow);

        var diskPanel = new Panel("[red]Disk: 85%[/]")
            .Header("Storage")
            .BorderColor(Color.Red);

        metricsGrid.AddRow(cpuPanel, memPanel, diskPanel);

        mainGrid.AddRow(metricsGrid);

        AnsiConsole.Write(mainGrid);

        Console.ReadKey();
    }

    private void DrawHeader()
    {
        var font = FigletFont.Load("fonts/ANSI_Regular.flf");

        var title = new FigletText(font, "DinnerPlans")
            .Color(Color.MediumSpringGreen)
            .Centered();

        var date = new Text(DateTime.Now.ToString("MMMM dd, yyyy"))
            .Centered();
        var calendar = new Calendar(DateTime.Now)
            .BorderStyle(new Style(Color.GreenYellow))
            .RoundedBorder()
            .HideHeader()
            .AddCalendarEvent(DateTime.Now);
        var dateGroup = new Rows(date, calendar);

        var header = new Grid()
            .Expand()
            .AddColumns(2)
            .AddEmptyRow()
            .AddRow(title, dateGroup);

        AnsiConsole.Write(header);
    }

    private Choice DrawMainMenu()
    {
        Choice[] choices =
        [
            new Choice("Generate a meal plan", GenerateMealPlan),
            new Choice("Exit", () => Environment.Exit(0))
        ];

        var mainMenu = new SelectionPrompt<Choice>()
            .Title("What would you like to do?")
            .AddChoices(choices)
            .UseConverter(x => x.Text);

        return AnsiConsole.Prompt(mainMenu);
    }

    private void GenerateMealPlan()
    {
        AnsiConsole.Status()
            .Start("Generating meal plan...", ctx =>
            {
                Thread.Sleep(2000);
            });
    }
}