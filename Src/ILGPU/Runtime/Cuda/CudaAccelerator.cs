﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Compiler;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents the accelerator flags for a Cuda accelerator.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    [Flags]
    public enum CudaAcceleratorFlags
    {
        /// <summary>
        /// Automatic scheduling (default).
        /// </summary>
        ScheduleAuto = 0,

        /// <summary>
        /// Spin scheduling.
        /// </summary>
        ScheduleSpin = 1,

        /// <summary>
        /// Yield scheduling
        /// </summary>
        ScheduleYield = 2,

        /// <summary>
        /// Blocking synchronization as default scheduling.
        /// </summary>
        ScheduleBlockingSync = 4,
    }

    /// <summary>
    /// Represents a cache configuration of a device.
    /// </summary>
    public enum CudaCacheConfiguration
    {
        /// <summary>
        /// The default cache configuration.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Prefer shared cache.
        /// </summary>
        PreferShared = 1,

        /// <summary>
        /// Prefer L1 cache.
        /// </summary>
        PreferL1 = 2,

        /// <summary>
        /// Prefer shared or L1 cache.
        /// </summary>
        PreferEqual = 3
    }

    /// <summary>
    /// Represents a shared-memory configuration of a device.
    /// </summary>
    public enum CudaSharedMemoryConfiguration
    {
        /// <summary>
        /// The default shared-memory configuration.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Setup a bank size of 4 byte.
        /// </summary>
        FourByteBankSize = 1,

        /// <summary>
        /// Setup a bank size of 8 byte.
        /// </summary>
        EightByteBankSize = 2
    }

    /// <summary>
    /// Represents the type of a device pointer.
    /// </summary>
    public enum CudaMemoryType
    {
        /// <summary>
        /// Represents no known memory type.
        /// </summary>
        None = 0,
        /// <summary>
        /// Represents a host pointer.
        /// </summary>
        Host = 1,
        /// <summary>
        /// Represents a device pointer.
        /// </summary>
        Device = 2,
        /// <summary>
        /// Represents a pointer to a Cuda array.
        /// </summary>
        Array = 3,
        /// <summary>
        /// Represents a unified-memory pointer.
        /// </summary>
        Unified = 4,
    }

    /// <summary>
    /// Represents a Cuda accelerator.
    /// </summary>
    public sealed class CudaAccelerator : Accelerator
    {
        #region Static

        /// <summary>
        /// Represents the list of available Cuda accelerators.
        /// </summary>
        private static List<AcceleratorId> cudaAccelerators;

        /// <summary>
        /// Returns a list of available Cuda accelerators.
        /// </summary>
        public static IReadOnlyList<AcceleratorId> CudaAccelerators
        {
            get
            {
                if (cudaAccelerators == null)
                {
                    // Resolve all devices
                    CudaException.ThrowIfFailed(
                        CudaNativeMethods.cuDeviceGetCount(out int numDevices));
                    cudaAccelerators = new List<AcceleratorId>(numDevices);
                    for (int i = 0; i < numDevices; ++i)
                    {
                        CudaException.ThrowIfFailed(
                            CudaNativeMethods.cuDeviceGet(out int deviceId, i));
                        cudaAccelerators.Add(new AcceleratorId(AcceleratorType.Cuda, deviceId));
                    }
                }
                return cudaAccelerators;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static CudaAccelerator()
        {
            // Ensure that the driver api is initialized
            CudaNativeMethods.cuInit(0);
        }

        /// <summary>
        /// Resolves the memory type of the given device pointer.
        /// </summary>
        /// <param name="value">The device pointer to check.</param>
        /// <returns>The resolved memory type</returns>
        public static unsafe CudaMemoryType GetCudaMemoryType(IntPtr value)
        {
            // This functionality requires unified addresses (X64)
            Backend.EnsureRunningOnPlatform(TargetPlatform.X64);

            int data = 0;
            var err = CudaNativeMethods.cuPointerGetAttribute(
                    new IntPtr(Unsafe.AsPointer(ref data)),
                    PointerAttribute.CU_POINTER_ATTRIBUTE_MEMORY_TYPE,
                    value);
            if (err == CudaError.CUDA_ERROR_INVALID_VALUE)
                return CudaMemoryType.None;
            CudaException.ThrowIfFailed(err);
            return (CudaMemoryType)data;
        }

        #endregion

        #region Instance

        private IntPtr contextPtr;

        /// <summary>
        /// Constructs a new Cuda accelerator targeting the default device.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        public CudaAccelerator(Context context)
            : this(context, 0)
        { }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        public CudaAccelerator(Context context, int deviceId)
            : this(context, deviceId, CudaAcceleratorFlags.ScheduleAuto)
        { }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        /// <param name="flags">The accelerator flags.</param>
        public CudaAccelerator(Context context, int deviceId, CudaAcceleratorFlags flags)
            : base(context, AcceleratorType.Cuda)
        {
            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuCtxCreate_v2(out contextPtr, flags, deviceId));
            DeviceId = deviceId;

            SetupAccelerator();
        }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        public CudaAccelerator(Context context, IntPtr d3d11Device)
            : this(context, d3d11Device, CudaAcceleratorFlags.ScheduleAuto)
        { }

        /// <summary>
        /// Constructs a new Cuda accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        /// <param name="flags">The accelerator flags.</param>
        public CudaAccelerator(Context context, IntPtr d3d11Device, CudaAcceleratorFlags flags)
            : base(context, AcceleratorType.Cuda)
        {
            if (d3d11Device == IntPtr.Zero)
                throw new ArgumentNullException(nameof(d3d11Device));

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuD3D11CtxCreate_v2(out contextPtr, out int deviceId, flags, d3d11Device));
            MakeCurrentInternal();
            DeviceId = deviceId;

            SetupAccelerator();
        }

        /// <summary>
        /// Setups the accelerator name.
        /// </summary>
        private void SetupName()
        {
            const int MaxAcceleratorNameLength = 2048;
            var nameBuffer = new byte[MaxAcceleratorNameLength];
            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuDeviceGetName(nameBuffer, nameBuffer.Length, DeviceId));
            var endIdx = Array.FindIndex(nameBuffer, 0, c => c == 0);
            Name = Encoding.ASCII.GetString(nameBuffer, 0, endIdx);
        }

        /// <summary>
        /// Setups all required settings.
        /// </summary>
        private void SetupAccelerator()
        {
            SetupName();
            DefaultStream = CudaStream.Default;

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuDeviceComputeCapability(out uint major, out uint minor, DeviceId));
            Architecture = PTXBackend.GetArchitecture(major, minor);

            IntPtr total = IntPtr.Zero;
            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuDeviceTotalMem_v2(ref total, DeviceId));
            MemorySize = total.ToInt64();

            // Resolve max grid size
            MaxGridSize = new Index3(
                CudaNativeMethods.cuDeviceGetAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_X, DeviceId),
                CudaNativeMethods.cuDeviceGetAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Y, DeviceId),
                CudaNativeMethods.cuDeviceGetAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Z, DeviceId));

            // Resolve max group size
            MaxGroupSize = new Index3(
                CudaNativeMethods.cuDeviceGetAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_X, DeviceId),
                CudaNativeMethods.cuDeviceGetAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Y, DeviceId),
                CudaNativeMethods.cuDeviceGetAttribute(
                    DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Z, DeviceId));

            // Resolve max threads per group
            MaxThreadsPerGroup = CudaNativeMethods.cuDeviceGetAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK, DeviceId);

            // Resolve max shared memory per block
            MaxSharedMemoryPerGroup = CudaNativeMethods.cuDeviceGetAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_BLOCK, DeviceId);

            // Resolve total constant memory
            MaxConstantMemory = CudaNativeMethods.cuDeviceGetAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_TOTAL_CONSTANT_MEMORY, DeviceId);

            // Resolve clock rate
            ClockRate = CudaNativeMethods.cuDeviceGetAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_CLOCK_RATE, DeviceId);

            // Resolve warp size
            WarpSize = CudaNativeMethods.cuDeviceGetAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_WARP_SIZE, DeviceId);

            // Resolve number of multiprocessors
            NumMultiprocessors = CudaNativeMethods.cuDeviceGetAttribute(
                DeviceAttribute.CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT, DeviceId);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the native Cuda-context ptr.
        /// </summary>
        public IntPtr ContextPtr => contextPtr;

        /// <summary>
        /// Returns the device id.
        /// </summary>
        public int DeviceId { get; }

        /// <summary>
        /// Returns the PTX architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; private set; }

        /// <summary>
        /// Returns the max group size.
        /// </summary>
        public Index3 MaxGroupSize { get; private set; }

        /// <summary>
        /// Returns the clock rate.
        /// </summary>
        public int ClockRate { get; private set; }

        /// <summary>
        /// Gets or sets the current shared-memory configuration.
        /// </summary>
        public CudaSharedMemoryConfiguration SharedMemoryConfiguration
        {
            get
            {
                MakeCurrentInternal();
                CudaException.ThrowIfFailed(
                    CudaNativeMethods.cuCtxGetSharedMemConfig(out CudaSharedMemoryConfiguration result));
                return result;
            }
            set
            {
                MakeCurrentInternal();
                if (value < CudaSharedMemoryConfiguration.Default ||
                    value > CudaSharedMemoryConfiguration.EightByteBankSize)
                    throw new ArgumentOutOfRangeException(nameof(value));
                CudaException.ThrowIfFailed(
                    CudaNativeMethods.cuCtxSetSharedMemConfig(value));
            }
        }

        /// <summary>
        /// Gets or sets the current cache configuration.
        /// </summary>
        public CudaCacheConfiguration CacheConfiguration
        {
            get
            {
                MakeCurrentInternal();
                CudaException.ThrowIfFailed(
                    CudaNativeMethods.cuCtxGetCacheConfig(out CudaCacheConfiguration result));
                return result;
            }
            set
            {
                MakeCurrentInternal();
                if (value < CudaCacheConfiguration.Default ||
                    value > CudaCacheConfiguration.PreferEqual)
                    throw new ArgumentOutOfRangeException(nameof(value));
                CudaException.ThrowIfFailed(
                    CudaNativeMethods.cuCtxSetCacheConfig(value));
            }
        }

        #endregion

        #region Methods

        /// <summary cref="Accelerator.CreateExtension{TExtension, TExtensionProvider}(TExtensionProvider)"/>
        public override TExtension CreateExtension<TExtension, TExtensionProvider>(TExtensionProvider provider)
        {
            return provider.CreateCudaExtension(this);
        }

        /// <summary cref="Accelerator.CreateBackend"/>
        public override Backend CreateBackend()
        {
            return new PTXBackend(Context, Architecture);
        }

        /// <summary>
        /// Makes this accelerator the current one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MakeCurrentInternal()
        {
            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuCtxSetCurrent(contextPtr));
        }

        /// <summary cref="Accelerator.Allocate{T, TIndex}(TIndex)"/>
        public override MemoryBuffer<T, TIndex> Allocate<T, TIndex>(TIndex extent)
        {
            MakeCurrentInternal();
            return new CudaMemoryBuffer<T, TIndex>(this, extent);
        }

        /// <summary cref="Accelerator.LoadKernel(CompiledKernel)"/>
        public override Kernel LoadKernel(CompiledKernel kernel)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            return new CudaKernel(
                this,
                kernel,
                GenerateKernelLauncherMethod(kernel, 0));
        }

        /// <summary cref="Accelerator.LoadImplicitlyGroupedKernel(CompiledKernel, int)"/>
        public override Kernel LoadImplicitlyGroupedKernel(
            CompiledKernel kernel,
            int customGroupSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (customGroupSize < 0 || customGroupSize > MaxThreadsPerGroup)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));
            if (kernel.EntryPoint.IsGroupedIndexEntry)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedExplicitlyGroupedKernel);
            return new CudaKernel(
                this,
                kernel,
                GenerateKernelLauncherMethod(kernel, customGroupSize));
        }

        /// <summary cref="Accelerator.LoadAutoGroupedKernel(CompiledKernel, out int, out int)"/>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "The object must not be disposed here")]
        public override Kernel LoadAutoGroupedKernel(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (kernel.EntryPoint.IsGroupedIndexEntry)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedExplicitlyGroupedKernel);

            var result = new CudaKernel(this, kernel, null);
            groupSize = EstimateGroupSizeInternal(result, 0, 0, out minGridSize);
            result.Launcher = GenerateKernelLauncherMethod(kernel, groupSize);
            return result;
        }

        /// <summary cref="Accelerator.CreateStream"/>
        public override AcceleratorStream CreateStream()
        {
            return new CudaStream();
        }

        /// <summary cref="Accelerator.Synchronize"/>
        public override void Synchronize()
        {
            MakeCurrentInternal();
            CudaException.ThrowIfFailed(CudaNativeMethods.cuCtxSynchronize());
        }

        /// <summary cref="Accelerator.MakeCurrent"/>
        public override void MakeCurrent()
        {
            MakeCurrentInternal();
        }

        /// <summary>
        /// Queries the amount of free memory.
        /// </summary>
        /// <returns>The amount of free memory in bytes.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method implies a native method invocation")]
        public long GetFreeMemory()
        {
            MakeCurrent();
            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuMemGetInfo_v2(out IntPtr free, out IntPtr total));
            return free.ToInt64();
        }

        #endregion

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeer(Accelerator)"/>
        public override bool CanAccessPeer(Accelerator otherAccelerator)
        {
            var cudaAccelerator = otherAccelerator as CudaAccelerator;
            if (cudaAccelerator == null)
                return false;

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuDeviceCanAccessPeer(out int canAccess, DeviceId, cudaAccelerator.DeviceId));
            return canAccess != 0;
        }

        /// <summary cref="Accelerator.EnablePeerAccess(Accelerator)"/>
        public override void EnablePeerAccess(Accelerator otherAccelerator)
        {
            if (HasPeerAccess(otherAccelerator))
                return;

            var cudaAccelerator = otherAccelerator as CudaAccelerator;
            if (cudaAccelerator == null)
                throw new InvalidOperationException(
                    RuntimeErrorMessages.CannotEnablePeerAccessToDifferentAcceleratorKind);

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuCtxEnablePeerAccess(cudaAccelerator.ContextPtr, 0));
            CachedPeerAccelerators.Add(otherAccelerator);
        }

        /// <summary cref="Accelerator.DisablePeerAccess(Accelerator)"/>
        public override void DisablePeerAccess(Accelerator otherAccelerator)
        {
            if (!HasPeerAccess(otherAccelerator))
                return;

            var cudaAccelerator = otherAccelerator as CudaAccelerator;
            Debug.Assert(cudaAccelerator != null, "Invalid EnablePeerAccess method");

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuCtxDisablePeerAccess(cudaAccelerator.ContextPtr));
            CachedPeerAccelerators.Remove(otherAccelerator);
        }

        #endregion

        #region General Launch Methods

        /// <summary>
        /// Generates a dynamic kernel-launcher method that will be just-in-time compiled
        /// during the first invocation. Using the generated launcher lowers the overhead
        /// for kernel launching dramatically, since unnecessary operations (like boxing)
        /// can be avoided.
        /// </summary>
        /// <param name="kernel">The kernel to generate a launcher for.</param>
        /// <param name="customGroupSize">The custom group size used for automatic blocking.</param>
        /// <returns>The generated launcher method.</returns>
        private MethodInfo GenerateKernelLauncherMethod(CompiledKernel kernel, int customGroupSize)
        {
            var entryPoint = kernel.EntryPoint;

            if (customGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));

            if (entryPoint.IsGroupedIndexEntry)
            {
                if (customGroupSize > 0)
                    throw new InvalidOperationException(RuntimeErrorMessages.InvalidCustomGroupSize);
            }
            else if (customGroupSize == 0)
                customGroupSize = WarpSize;

            var kernelParamTypes = entryPoint.CreateCustomParameterTypes();
            int numKernelParams = kernelParamTypes.Length;
            var groupSizeOffset = entryPoint.IsGroupedIndexEntry ? 0 : 1;
            var funcParamTypes = new Type[numKernelParams + Kernel.KernelParameterOffset];

            // Launcher(Kernel, AcceleratorStream, [Index], ...)
            funcParamTypes[Kernel.KernelInstanceParamIdx] = typeof(Kernel);
            funcParamTypes[Kernel.KernelStreamParamIdx] = typeof(AcceleratorStream);
            funcParamTypes[Kernel.KernelParamDimensionIdx] = entryPoint.KernelIndexType;
            kernelParamTypes.CopyTo(funcParamTypes, Kernel.KernelParameterOffset);

            // Create the actual launcher method
            var func = new DynamicMethod(kernel.EntryName, typeof(void), funcParamTypes, typeof(KernelLauncherBuilder));
            var funcParams = func.GetParameters();
            var ilGenerator = func.GetILGenerator();

            // Allocate array of pointers as kernel argument(s)
            var kernelArguments = ilGenerator.DeclareLocal(typeof(IntPtr));
            ilGenerator.Emit(OpCodes.Ldc_I4, IntPtr.Size * (numKernelParams + groupSizeOffset));
            ilGenerator.Emit(OpCodes.Conv_U);
            ilGenerator.Emit(OpCodes.Localloc);
            ilGenerator.Emit(OpCodes.Stloc, kernelArguments);

            var newIntPtr = typeof(IntPtr).GetConstructor(new Type[] { typeof(void).MakePointerType() });

            // Add the actual dispatch-size information to the kernel parameters
            if (groupSizeOffset > 0)
            {
                // Load data pointer
                ilGenerator.Emit(OpCodes.Ldloc, kernelArguments);

                // Store custom dispatch-size information
                ilGenerator.Emit(OpCodes.Ldarga, Kernel.KernelParamDimensionIdx);
                ilGenerator.Emit(OpCodes.Conv_I);
                ilGenerator.Emit(OpCodes.Newobj, newIntPtr);

                // Store param address
                ilGenerator.Emit(OpCodes.Stind_I);
            }

            // Fill uniform variables
            for (int i = 0; i < numKernelParams; ++i)
            {
                // Load data pointer
                ilGenerator.Emit(OpCodes.Ldloc, kernelArguments);
                ilGenerator.Emit(OpCodes.Ldc_I4, (i + groupSizeOffset) * IntPtr.Size);
                ilGenerator.Emit(OpCodes.Add);

                // Store param address in native memory
                var param = funcParams[i + Kernel.KernelParameterOffset];
                ilGenerator.Emit(OpCodes.Ldarga, param.Position);
                ilGenerator.Emit(OpCodes.Conv_I);
                ilGenerator.Emit(OpCodes.Newobj, newIntPtr);

                // Store param address
                ilGenerator.Emit(OpCodes.Stind_I);
            }

            // Compute sizes of dynamic-shared variables
            var sharedMemSize = KernelLauncherBuilder.EmitSharedMemorySizeComputation(
                entryPoint,
                ilGenerator,
                paramIdx => funcParams[paramIdx + Kernel.KernelParameterOffset]);

            // Emit kernel launch

            // Load function ptr
            KernelLauncherBuilder.EmitLoadKernelArgument<CudaKernel>(Kernel.KernelInstanceParamIdx, ilGenerator);
            ilGenerator.Emit(
                OpCodes.Call,
                typeof(CudaKernel).GetProperty(
                    nameof(CudaKernel.FunctionPtr),
                    BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false));

            // Load dimensions
            KernelLauncherBuilder.EmitLoadDimensions(
                entryPoint,
                ilGenerator,
                Kernel.KernelParamDimensionIdx,
                () => { },
                customGroupSize);

            // Load shared-mem size
            ilGenerator.Emit(OpCodes.Ldloc, sharedMemSize);

            // Load stream
            KernelLauncherBuilder.EmitLoadAcceleratorStream<CudaStream>(Kernel.KernelStreamParamIdx, ilGenerator);
            ilGenerator.Emit(
                OpCodes.Call,
                typeof(CudaStream).GetProperty(
                    nameof(CudaStream.StreamPtr),
                    BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false));

            // Load kernel args
            ilGenerator.Emit(OpCodes.Ldloc, kernelArguments);

            // Load additional kernel args
            ilGenerator.Emit(OpCodes.Ldsfld, typeof(IntPtr).GetField(
                nameof(IntPtr.Zero),
                BindingFlags.Public | BindingFlags.Static));

            // Dispatch kernel
            ilGenerator.Emit(
                OpCodes.Call,
                typeof(CudaNativeMethods).GetMethod(
                    nameof(CudaNativeMethods.cuLaunchKernel),
                    BindingFlags.Public | BindingFlags.Static));

            // Emit ThrowIfFailed
            ilGenerator.Emit(
                OpCodes.Call,
                typeof(CudaException).GetMethod(
                    nameof(CudaException.ThrowIfFailed),
                    BindingFlags.Public | BindingFlags.Static));

            ilGenerator.Emit(OpCodes.Ret);

            return func;
        }

        #endregion

        #region Occupancy

        /// <summary cref="Accelerator.EstimateMaxActiveGroupsPerMultiprocessor(Kernel, int, int)"/>
        protected override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
        {
            var cudaKernel = kernel as CudaKernel;
            if (cudaKernel == null)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuOccupancyMaxActiveBlocksPerMultiprocessor(
                    out int numGroups,
                    cudaKernel.FunctionPtr,
                    groupSize,
                    new IntPtr(dynamicSharedMemorySizeInBytes)));
            return numGroups;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize)
        {
            var cudaKernel = kernel as CudaKernel;
            if (cudaKernel == null)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            Backend.EnsureRunningOnNativePlatform();

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuOccupancyMaxPotentialBlockSize(
                    out minGridSize,
                    out int groupSize,
                    cudaKernel.FunctionPtr,
                    new CudaNativeMethods.CUoccupancyB2DSize(
                        targetGroupSize => new IntPtr(computeSharedMemorySize(targetGroupSize))),
                    IntPtr.Zero,
                    maxGroupSize));
            return groupSize;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            var cudaKernel = kernel as CudaKernel;
            if (cudaKernel == null)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            Backend.EnsureRunningOnNativePlatform();

            CudaException.ThrowIfFailed(
                CudaNativeMethods.cuOccupancyMaxPotentialBlockSize(
                    out minGridSize,
                    out int groupSize,
                    cudaKernel.FunctionPtr,
                    null,
                    new IntPtr(dynamicSharedMemorySizeInBytes),
                    maxGroupSize));
            return groupSize;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (contextPtr != IntPtr.Zero)
            {
                CudaException.ThrowIfFailed(CudaNativeMethods.cuCtxDestroy_v2(contextPtr));
                contextPtr = IntPtr.Zero;
            }
        }

        #endregion
    }
}
