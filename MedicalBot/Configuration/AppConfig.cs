namespace MedicalBot.Configuration;

public class AppConfig
{
    public string BotToken { get; set; }
    public string CashUrl { get; set; }      
    public string PatientsUrl { get; set; }
    public string ScheduleUrl { get; set; } 
    public long[] DirectorIds { get; set; }
    public Dictionary<string, string> ConnectionStrings { get; set; }
}