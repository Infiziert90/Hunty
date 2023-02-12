using System.Text.RegularExpressions;
using Dalamud;
using static System.Text.RegularExpressions.Match;

namespace Hunty.Logic;

public static partial class Reg
{
    public static Match Match(string message, ClientLanguage language)
    {

        return language switch
        {
            ClientLanguage.English => English.Match(message),
            ClientLanguage.German => German.Match(message),
            ClientLanguage.French => French.Match(message),
            ClientLanguage.Japanese => Japanese.Match(message),
            _ => Empty
        };
    }


    public abstract class Base
    {
        protected static Match Check(Regex killed, Regex complete, string message)
        {
            var m = killed.Match(message);
            if (m.Success) return m;

            m = complete.Match(message);
            return m.Success ? m : Empty;
        }
    }

    private partial class English : Base
    {
        [GeneratedRegex(@"^Record of (?<mob>.*?) kill \((?<killed>\d)/\d\) added to hunting log entry (?<job>.*?) \d{2}.")]
        private static partial Regex HuntingKilled();
        [GeneratedRegex(@"^Hunting log entry for (?<mob>.*?)s complete!")] // plural is used, so ending on s
        private static partial Regex HuntingMobComplete();

        public static Match Match(string message) => Check(HuntingKilled(), HuntingMobComplete(), message);
    }
    
    private partial class German : Base
    {
        [GeneratedRegex(@"^Not Implemented")]
        private static partial Regex HuntingKilled();
        [GeneratedRegex(@"^Not Implemented")]
        private static partial Regex HuntingMobComplete();

        public static Match Match(string message) => Check(HuntingKilled(), HuntingMobComplete(), message);
    }
    
    private partial class French : Base
    {
        [GeneratedRegex(@"^Not Implemented")]
        private static partial Regex HuntingKilled();
        [GeneratedRegex(@"^Not Implemented")]
        private static partial Regex HuntingMobComplete();

        public static Match Match(string message) => Check(HuntingKilled(), HuntingMobComplete(), message);
    }
    
    private partial class Japanese : Base
    {
        [GeneratedRegex(@"^Not Implemented")]
        private static partial Regex HuntingKilled();
        [GeneratedRegex(@"^Not Implemented")]
        private static partial Regex HuntingMobComplete();

        public static Match Match(string message) => Check(HuntingKilled(), HuntingMobComplete(), message);
    }
}