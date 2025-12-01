using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class FavoriteDtoForCreate
    {
        public string FirebaseUid { get; init; }
        public string ProductPlatformId { get; init; }
    }
}
