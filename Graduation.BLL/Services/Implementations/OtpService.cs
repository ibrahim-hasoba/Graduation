using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Implementations
{
  public class OtpService : IOtpService
  {
    private readonly DatabaseContext _context;

    public OtpService(DatabaseContext context)
    {
      _context = context;
    }

    public async Task<string> GenerateOtpAsync(string email, string purpose = "email_verification", int ttlMinutes = 10)
    {
      // Generate 6-digit numeric code
      var rnd = new Random();
      var code = rnd.Next(100000, 999999).ToString();

      // Expire existing OTPs for this email+purpose
      var existing = await _context.EmailOtps
          .Where(e => e.Email == email && e.Purpose == purpose && !e.Consumed)
          .ToListAsync();

      foreach (var e in existing)
      {
        e.Consumed = true;
      }

      var otp = new EmailOtp
      {
        Email = email,
        Code = code,
        Purpose = purpose,
        ExpiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes),
        Consumed = false
      };

      _context.EmailOtps.Add(otp);
      await _context.SaveChangesAsync();

      return code;
    }

    public async Task<bool> ValidateOtpAsync(string email, string code, string purpose = "email_verification")
    {
      var otp = await _context.EmailOtps
          .Where(e => e.Email == email && e.Purpose == purpose && !e.Consumed)
          .OrderByDescending(e => e.CreatedAt)
          .FirstOrDefaultAsync();

      if (otp == null) return false;
      if (otp.ExpiresAt < DateTime.UtcNow) return false;
      if (otp.Code != code) return false;

      otp.Consumed = true;
      await _context.SaveChangesAsync();
      return true;
    }
  }
}
