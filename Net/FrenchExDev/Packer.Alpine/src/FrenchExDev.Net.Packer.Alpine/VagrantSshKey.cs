namespace FrenchExDev.Net.Packer.Alpine;

/// <summary>
/// The Vagrant insecure public SSH key, used during Packer provisioning.
/// Vagrant replaces this with a secure key on first <c>vagrant up</c>.
/// </summary>
public static class VagrantSshKey
{
    public const string InsecurePublicKey =
        "ssh-rsa AAAAB3NzaC1yc2EAAAABIwAAAQEA6NF8iallvQVp22WDkTkyrtvp9eWW6A8YVr+kz4TjGYe7gHzIw+niNltGEFHzD8+v1I2YJ6oXevct1YeS0o9HZyN1Q9qgCgzUFtdOKLv6IedplJWTNSLQgxSxF7GAnfu6zIyMpVKwkxvEn1Iyjh7eMlK5M1l36/1cG8z5e3Uat2d/TbxPwLk7Pb/LXs4Wx7LWe58fR/ogaITFk60P8w2VSLnUr0RjK1A+NHi1peqg99bR+MYEwJpmpKFJYy2efvBOtH2CzV3bPCY3HJWq66RJk0oGsE8N/jsSXMDOT/VZxBLk0IOvR5ZYFJRISVlOoiJJhKJBKhCSRlfUa2V5khGlw== vagrant insecure public key";
}
