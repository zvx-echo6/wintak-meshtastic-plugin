// Stub for nanopb proto reflection - Meshtastic proto files import nanopb.proto
// but we don't need the nanopb options in C#. This provides a no-op implementation.
using Google.Protobuf.Reflection;

/// <summary>
/// Stub for NanopbReflection to satisfy the generated protobuf code.
/// Meshtastic uses nanopb options in their proto files which aren't relevant for C#.
/// </summary>
public static class NanopbReflection
{
    private static FileDescriptor _descriptor;

    public static FileDescriptor Descriptor
    {
        get
        {
            if (_descriptor == null)
            {
                // Create a minimal descriptor
                _descriptor = FileDescriptor.FromGeneratedCode(
                    System.Convert.FromBase64String("Cg1uYW5vcGIucHJvdG8="),
                    new FileDescriptor[] { },
                    null);
            }
            return _descriptor;
        }
    }
}
