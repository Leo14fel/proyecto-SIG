using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Web.Models;

public sealed record UserAdministrationItem(int Id, string Login, string Nombre, string Roles, bool Activo);

public sealed class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = string.Empty;
}
