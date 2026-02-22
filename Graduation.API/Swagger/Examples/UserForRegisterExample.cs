using Shared.DTOs;

namespace Graduation.API.Swagger.Examples
{
    public class UserForRegisterExample : IExampleProvider
    {
        public object GetExample()
            => new UserForRegisterDto
            {
                FirstName = "ibrahim",
                LastName = "adham",
                Email = "hasoba@example.com",
                Password = "StrongP@ssw0rd",
                ConfirmPassword = "StrongP@ssw0rd"
            };
    }
}
