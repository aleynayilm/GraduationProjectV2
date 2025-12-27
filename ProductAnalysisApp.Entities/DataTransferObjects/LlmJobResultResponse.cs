using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class LlmJobResultResponse
    {
        public string Status { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
    }
}
