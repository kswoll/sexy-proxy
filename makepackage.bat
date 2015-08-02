REM "C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe" SexyReact.Fody.sln

cd Nuget

..\packages\NugetUtilities.1.0.5\UpdateVersion.exe SexyProxy.nuspec -Increment

mkdir build
cd build

copy ..\SexyProxy.nuspec .

mkdir lib
mkdir lib\net45
REM mkdir lib\Xamarin.iOS10
REM mkdir lib\MonoAndroid
REM mkdir "lib\portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10"

copy ..\..\SexyProxy.Net45\bin\Debug\SexyProxy.* lib\net45
REM copy ..\artifacts\ios\SexyReact.Fody.Helpers.* lib\Xamarin.iOS10
REM copy ..\..\ReactiveUI.Fody.1.0.26\lib\Xamarin.iOS10\ReactiveUI.Fody.Helpers.* lib\Xamarin.iOS10
REM copy ..\..\..\SexyReact.Fody.Helpers.Net45\bin\Debug\SexyReact.Fody.Helpers.* lib\net45
REM copy ..\..\ReactiveUI.Fody.Helpers.Pcl\bin\Debug\ReactiveUI.Fody.Helpers.* "lib\portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10"
REM copy ..\..\ReactiveUI.Fody.Helpers.Android\bin\Debug\ReactiveUI.Fody.Helpers.* lib\MonoAndroid
REM copy ..\..\ReactiveUI.Fody.1.0.26\lib\MonoAndroid\ReactiveUI.Fody.Helpers.* lib\MonoAndroid


..\nuget pack SexyProxy.nuspec

copy *.nupkg ..

cd ..
rmdir build /S /Q
cd ..