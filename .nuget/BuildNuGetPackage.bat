pushd ..\Src
msbuild ILGPU.sln /p:Configuration=Debug
msbuild ILGPU.sln /p:Configuration=Release
popd
nuget pack ILGPU.nuspec
