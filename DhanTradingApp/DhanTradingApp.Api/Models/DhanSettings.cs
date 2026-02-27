using System.ComponentModel.DataAnnotations;

namespace DhanTradingApp.Api.Models
{
    public class DhanSettings
    {
        [Key]
        public int Id { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }
}
