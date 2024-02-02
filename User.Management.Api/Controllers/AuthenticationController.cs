using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using User.Management.Api.Models;
using User.Management.Api.Models.Authentication.Login;
using User.Management.Api.Models.Authentication.SignUp;
using User.Management.Service.Models;
using User.Management.Service.Services;

namespace User.Management.Api.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthenticationController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IEmailService emailService, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<ActionResult> Register([FromBody] RegisterUser registerUser, string role)
        {
            // check User exist
            var userExist = await _userManager.FindByEmailAsync(registerUser.Email);
            if (userExist != null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new Response { Status = "Error", Message = "User Already Exists" });
            }

            // Add User in the database
            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.Username
            };

            if (await _roleManager.RoleExistsAsync(role))
            {
                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "User not created" });
                }

                // add role to the user
                await _userManager.AddToRoleAsync(user, role);

                // add token to verify the email
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new { token, email = user.Email }, Request.Scheme);
                var message = new Message(new string[] { user.Email! }, "Confirmation email link", confirmationLink!);
                _emailService.SendEmail(message);

                return StatusCode(StatusCodes.Status200OK,
                    new Response { Status = "Success", Message = $"User created & Email Sent to {user.Email} Successfully" });
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "This Role does not exist" });
            }
        }

        [HttpGet("ConfirmEmail")]
        public async Task<ActionResult> ConfirmEmail(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if(user != null)
            {
                var result = await _userManager.ConfirmEmailAsync(user, token);
                if(result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status200OK,
                        new Response { Status = "Success", Message = "Email verified Successfully" });
                }
            }
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Response { Status = "Error", Message = "This User does not exist" });
        }

        [HttpPost]
        [Route("Login")]
        public async Task<ActionResult> Login([FromBody] LoginModel loginModel)
        {
            // checking the user
            var user = await _userManager.FindByNameAsync(loginModel.Username);

            if(user != null && await _userManager.CheckPasswordAsync(user, loginModel.Password))
            {
                // claimlist creation
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

                // add role to the claim list
                var userRoles = await _userManager.GetRolesAsync(user);
                foreach(var role in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, role));
                }

                // generate the token with claims
                var jwtToken = GetToken(authClaims);

                // returning the token
                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                    expiration = jwtToken.ValidTo
                });
            }
            return Unauthorized();
        }

        // TODO : Not work due to message that couldn't be sent
        //[HttpPost]
        //[AllowAnonymous]
        //public async Task<IActionResult> ForgotPassword([Required] string email)
        //{
        //    var user = await _userManager.FindByEmailAsync(email);

        //    if(user != null)
        //    {
        //        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        //        var forgotPasswordLink = Url.Action(
        //            "ResetPassword", "Authentication",
        //            new {
        //                token,
        //                email = user.Email
        //            },
        //            Request.Scheme);

        //        var message = new Message(new string[] {user.Email!}, "Forgot Password link", forgotPasswordLink!);

        //        _emailService.SendEmail(message);

        //        return StatusCode(StatusCodes.Status200OK,
        //            new Response { Status = "Success", Message = $"{forgotPasswordLink}" });
        //    }

        //    return StatusCode(StatusCodes.Status400BadRequest,
        //            new Response
        //            {
        //                Status = "error",
        //                Message =
        //            "Could not send link to email, please try again."
        //            });
        //}

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigninKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddHours(3),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigninKey, SecurityAlgorithms.HmacSha256)
            );

            return token;
        }
    }
}
