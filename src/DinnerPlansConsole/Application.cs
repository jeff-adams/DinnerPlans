using Microsoft.Extensions.Configuration;
using Spectre.Console;

public class Application(IConfiguration configuration)
{
    private readonly IConfiguration config = configuration;

    public void Run()
    {
        AnsiConsole.Clear();

        var font = FigletFont.Load("fonts/ANSI_Regular.flf");
        var banner = new FigletText(font, "DinnerPlans")
            .Centered()
            .Color(Color.MediumSpringGreen);
        var divider = new Rule()
            .RuleStyle(Style.Parse("grey"))
            .Centered();
            
        AnsiConsole.Write(banner);
        AnsiConsole.Write(divider);
    }
}