# BuildSystem.Core

Game data build-system written in .NET Core that uses C# as meta-data.
You can use most of C# to initialize/construct data etc.. However once
the C# files that define the game-data are compiled and instanciated the
following things will be done by the BuildSystem:

- It will search for ``IDataRoot`` and will treat it as the root of the DataUnit
- It recognizes the following types:
  - ``DataUnit``; A reference to a ``IDataRoot`` that exists in another ``GameData...DLL``
  - ``FileId``; An Id that can be retrieved interacting with BigfileManager
  - ``s8``/``s16``/``s32``/``s64``
  - ``u8``/``u16``/``u32``/``u64``
  - ``fx16``/``fx32``
  - ``f32``/``f64``; single and double float precision
  - ``FRect``/``IRect``; floating point and integer rectangle (left, right, top, bottom)   - - ``Array<>``; C style array of any type listed here
compound
  - ``FSize``/``Size``; floating point and integer size (width, height) compound
  - ``FVec2``/``FVec3``/``FVec4``;
  - ``FMat22``/``FMat33``/``FMat44``;
  - ``Color``; 32-bit RGBA color
  - ``LString``; Localized string
 - Anything derived from ``IAtom`` (system types s8/s16/..., see above)
 - Anything derived from ``ICompound`` (mapped to a struct or class in C++)
 - Anything derived from ``IDataCompiler``
  - ```MeshCompiler("objects/rock.asset")```
  - ```TextureCompiler("textures/logo.TGA", ETexFormat.BC5_UNORM_BLOCK)```
  - ```MaterialCompiler("materials/stone.mat")```
  - ```ShaderCompiler("shaders/shadow.vs", EShaderFormat.VS_SPIRV)```
  - ```ShaderCompiler("shaders/shadow.ps", EShaderFormat.PS_SPIRV)```

In your game runtime you can use the `C++` library `cgamedata` to use it.
