using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using PCHardwareMonitor;

if (args.Length < 2) throw new ArgumentException("Usage: init <key-directory> [backup-directory] | backup <key-directory> <backup-directory> | verify-key <key-directory> | sign <key-directory> <installer> <version> | verify-release <installer> <version>");
if (args[0] == "verify-release" && args.Length == 3)
{
    string installer = Path.GetFullPath(args[1]);
    using var input = new FileStream(installer, FileMode.Open, FileAccess.Read, FileShare.Read);
    string hash = Convert.ToHexString(SHA256.HashData(input));
    var signature = new FileInfo(installer + ".signature.json");
    if (signature.Length > UpdateSignatureFormat.MaximumEnvelopeBytes) throw new IOException("Signature file too large.");
    UpdateSignatureFormat.Verify(File.ReadAllBytes(signature.FullName), Convert.FromBase64String(UpdateSigningKey.PublicKeyBase64),
        args[2], Path.GetFileName(installer), input.Length, hash);
    Console.WriteLine("Release signature and installer hash verified.");
    return;
}
string keyDirectory = Path.GetFullPath(args[1]);
for (DirectoryInfo? parent = new(keyDirectory); parent != null; parent = parent.Parent)
    if (Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git")))
        throw new IOException("Private keys must stay outside a Git repository.");
string privatePath = Path.Combine(keyDirectory, "release-private.pk8");
string publicPath = Path.Combine(keyDirectory, "release-public.spki");
if (args[0] == "init")
{
    SecureDirectory(keyDirectory);
    if (File.Exists(privatePath) || File.Exists(publicPath)) throw new IOException("Signing keys already exist; refusing to replace them.");
    using var generated = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    byte[] privateBytes = generated.ExportPkcs8PrivateKey();
    try
    {
        WriteNew(privatePath, privateBytes);
        WriteNew(publicPath, generated.ExportSubjectPublicKeyInfo());
        if (args.Length > 2)
        {
            string backup = Path.GetFullPath(args[2]);
            for (DirectoryInfo? parent = new(backup); parent != null; parent = parent.Parent)
                if (Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git")))
                    throw new IOException("Backup keys must stay outside Git.");
            SecureDirectory(backup);
            WriteNew(Path.Combine(backup, "release-private.pk8"), privateBytes);
            WriteNew(Path.Combine(backup, "release-public.spki"), generated.ExportSubjectPublicKeyInfo());
        }
        Console.WriteLine("Signing key created. Public key ID: " + UpdateSignatureFormat.KeyId(generated.ExportSubjectPublicKeyInfo()));
    }
    finally { CryptographicOperations.ZeroMemory(privateBytes); }
}
else if (args[0] == "backup" && args.Length == 3)
{
    string backup = Path.GetFullPath(args[2]);
    for (DirectoryInfo? parent = new(backup); parent != null; parent = parent.Parent)
        if (Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git")))
            throw new IOException("Backup keys must stay outside Git.");
    SecureDirectory(backup);
    byte[] privateBytes = File.ReadAllBytes(privatePath);
    try
    {
        WriteNew(Path.Combine(backup, "release-private.pk8"), privateBytes);
        WriteNew(Path.Combine(backup, "release-public.spki"), File.ReadAllBytes(publicPath));
        Console.WriteLine("Private signing backup created outside Git.");
    }
    finally { CryptographicOperations.ZeroMemory(privateBytes); }
}
else if (args[0] == "verify-key")
{
    byte[] bytes = File.ReadAllBytes(privatePath);
    try
    {
        using var key = ECDsa.Create(); key.ImportPkcs8PrivateKey(bytes, out int consumed);
        byte[] publicKey = File.ReadAllBytes(publicPath);
        if (consumed != bytes.Length || !publicKey.SequenceEqual(Convert.FromBase64String(UpdateSigningKey.PublicKeyBase64)) ||
            !key.ExportSubjectPublicKeyInfo().SequenceEqual(publicKey)) throw new IOException("Embedded signing key does not match.");
        byte[] challenge = RandomNumberGenerator.GetBytes(32);
        byte[] signature = key.SignData(challenge, HashAlgorithmName.SHA256);
        using var verifier = ECDsa.Create(); verifier.ImportSubjectPublicKeyInfo(publicKey, out _);
        if (!verifier.VerifyData(challenge, signature, HashAlgorithmName.SHA256)) throw new IOException("Signing-key self-test failed.");
        Console.WriteLine("Signing-key self-test passed. Key ID: " + UpdateSignatureFormat.KeyId(publicKey));
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
}
else if (args[0] == "sign" && args.Length == 4)
{
    string installer = Path.GetFullPath(args[2]);
    if (!Version.TryParse(args[3], out _) || Path.GetFileName(installer) != "Thermiqra_Setup_" + args[3] + ".exe")
        throw new IOException("Installer name must match the release version.");
    byte[] privateBytes = File.ReadAllBytes(privatePath);
    try
    {
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(privateBytes, out int consumed);
        if (consumed != privateBytes.Length || key.KeySize != 256) throw new IOException("Unexpected private key.");
        byte[] publicKey = File.ReadAllBytes(publicPath);
        if (!key.ExportSubjectPublicKeyInfo().SequenceEqual(publicKey) || !publicKey.SequenceEqual(Convert.FromBase64String(UpdateSigningKey.PublicKeyBase64)))
            throw new IOException("The private key does not match the public key embedded in Thermiqra.");
        using var input = new FileStream(installer, FileMode.Open, FileAccess.Read, FileShare.Read);
        string hash = Convert.ToHexString(SHA256.HashData(input));
        var payload = new UpdateSignedPayload(1, "Thermiqra", args[3], Path.GetFileName(installer), input.Length, hash);
        byte[] signature = UpdateSignatureFormat.Sign(payload, key);
        UpdateSignatureFormat.Verify(signature, publicKey, payload.Version, payload.FileName, input.Length, hash);
        if (File.Exists(installer + ".signature.json"))
            UpdateSignatureFormat.Verify(File.ReadAllBytes(installer + ".signature.json"), publicKey, payload.Version, payload.FileName, input.Length, hash);
        else WriteNew(installer + ".signature.json", signature);
        string checksum = hash + "  " + Path.GetFileName(installer) + "\n";
        if (File.Exists(installer + ".sha256"))
        { if (File.ReadAllText(installer + ".sha256") != checksum) throw new IOException("An inconsistent checksum file already exists."); }
        else WriteNew(installer + ".sha256", System.Text.Encoding.ASCII.GetBytes(checksum));
        Console.WriteLine("Signature created and verified. SHA-256: " + hash);
    }
    finally { CryptographicOperations.ZeroMemory(privateBytes); }
}
else throw new ArgumentException("Unknown signing command.");

static void WriteNew(string path, byte[] bytes)
{
    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    stream.Write(bytes); stream.Flush(true);
}

static void SecureDirectory(string path)
{
    for (DirectoryInfo? parent = new(path); parent != null; parent = parent.Parent)
        if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Signing-key path contains a link.");
    var directory = Directory.CreateDirectory(path);
    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Key directory cannot be a link.");
    var user = WindowsIdentity.GetCurrent().User!;
    var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
    foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
        acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
    directory.SetAccessControl(acl);
}
