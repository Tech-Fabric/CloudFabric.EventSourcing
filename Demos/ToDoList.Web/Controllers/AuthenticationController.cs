using System.Security.Claims;
using Htmx;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ToDoList.Models.RequestModels.UserAccounts;
using ToDoList.Services.Interfaces;
using ToDoList.Web.Extensions;

namespace ToDoList.Web.Controllers;


public class AuthenticationController : Controller
{
    private readonly IUserAccountsService _userAccountsService;

    public AuthenticationController(IUserAccountsService userAccountsService)
    {
        _userAccountsService = userAccountsService;
    }
    
    [HttpGet("header")]
    public async Task<IActionResult> Header()
    {
        return PartialView("_AuthenticationHeader");
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> Index([FromForm] AuthenticateUserRequest authenticationRequest, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (ModelState.IsValid)
        {
            var serviceResult = await _userAccountsService.AuthenticateUser(authenticationRequest, ct);

            if (serviceResult.Succeed)
            {
                var identity = new ClaimsIdentity(serviceResult.Result?.Claims, "Cookies");
                
                var authProperties = new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.AddDays(7),
                    IsPersistent = true,
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

                if (Request.IsHtmx())
                {
                    Response.Htmx(a => a.WithTrigger("authenticationStateChanged").Redirect("/"));
                    return Ok();
                }
                
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelErrorsFromServiceResult(serviceResult.ProblemDetails);
            }
        }

        return Request.IsHtmx()
            ? PartialView("Forms/_AuthenticationForm", authenticationRequest)
            : View();
    }

    [HttpPost("signout")]
    public new async Task<IActionResult> SignOut()
    {
        await HttpContext.SignOutAsync("Cookies");

        if (Request.IsHtmx())
        {
            Response.Htmx(a => a.WithTrigger("authenticationStateChanged").Redirect("/"));
            return Ok();
        }

        return RedirectToAction("Index", "Home");
    }
}