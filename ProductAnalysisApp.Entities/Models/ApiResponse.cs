using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.Models
{
    public class ApiResponse<T>
    {
        public string SessionId { get; set; }
        public long DurationMs { get; set; }
        public T Data { get; set; }
    }
}
