using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class LlmJobCreateResponse
    {
        public string JobId { get; set; }
        public string Status { get; set; }
    }
}
