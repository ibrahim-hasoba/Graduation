using System.Collections.Generic;

namespace Graduation.API.Errors
{
    public class ApiValidationErrorResponse:ApiResponse
    {
        public IEnumerable<string> Errors { get; set; }

        public ApiValidationErrorResponse():base(400)
        {

        }
    }
}
