using Shared.DTOs;

namespace Graduation.API.Swagger.Examples
{
    public class UserForLoginExample : IExampleProvider
    {
        public object GetExample()
            => new UserForLoginDto
            {
                Email = "hasoba@example.com",
                Password = "StrongP@ssw0rd"
            };
    }
}
