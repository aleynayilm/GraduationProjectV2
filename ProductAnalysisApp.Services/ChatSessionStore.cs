using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    public static class ChatSessionStore
    {
        public static Dictionary<string, List<ChatMessage>> Sessions { get; set; }
            = new Dictionary<string, List<ChatMessage>>();
    }
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
