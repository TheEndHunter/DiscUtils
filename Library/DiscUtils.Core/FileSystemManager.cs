//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using DiscUtils.Vfs;

namespace DiscUtils;

/// <summary>
/// FileSystemManager determines which file systems are present on a volume.
/// </summary>
/// <remarks>
/// The static detection methods detect default file systems.  To plug in additional
/// file systems, create an instance of this class and call RegisterFileSystems.
/// </remarks>
public static class FileSystemManager
{
    private static readonly List<VfsFileSystemFactory> _factories;

    /// <summary>
    /// Initializes a new instance of the FileSystemManager class.
    /// </summary>
    static FileSystemManager()
    {
        _factories = [];
    }

    /// <summary>
    /// Registers new file systems with an instance of this class.
    /// </summary>
    /// <param name="factory">The detector for the new file systems.</param>
    public static void RegisterFileSystems(VfsFileSystemFactory factory)
    {
        lock (_factories)
        {
            _factories.Add(factory);
        }
    }

    /// <summary>
    /// Registers new file systems detected in an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <remarks>
    /// To be detected, the <c>VfsFileSystemFactory</c> instances must be marked with the
    /// <c>VfsFileSystemFactoryAttribute</c>> attribute.
    /// </remarks>
    public static void RegisterFileSystems(Assembly assembly)
    {
        lock (_factories)
        {
            _factories.AddRange(DetectFactories(assembly));
        }
    }

    /// <summary>
    /// Detect which file systems are present on a volume.
    /// </summary>
    /// <param name="volume">The volume to inspect.</param>
    /// <returns>The list of file systems detected.</returns>
    public static ReadOnlyCollection<FileSystemInfo> DetectFileSystems(VolumeInfo volume)
    {
        using Stream s = volume.Open();
        return DoDetect(s, volume);
    }

    /// <summary>
    /// Detect which file systems are present in a stream.
    /// </summary>
    /// <param name="stream">The stream to inspect.</param>
    /// <returns>The list of file systems detected.</returns>
    public static ReadOnlyCollection<FileSystemInfo> DetectFileSystems(Stream stream)
    {
        return DoDetect(stream, null);
    }

    private static IEnumerable<VfsFileSystemFactory> DetectFactories(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            Attribute attrib = type.GetCustomAttribute<VfsFileSystemFactoryAttribute>(false);
            if (attrib == null)
            {
                continue;
            }

            yield return (VfsFileSystemFactory)Activator.CreateInstance(type);
        }
    }

    private static ReadOnlyCollection<FileSystemInfo> DoDetect(Stream stream, VolumeInfo volume)
    {
        var detectStream = new BufferedStream(stream);
        var detected = new List<FileSystemInfo>();

        lock (_factories)
        {
            foreach (var factory in _factories)
            {
                detected.AddRange(factory.Detect(detectStream, volume));
            }
        }

        return new(detected);
    }
}