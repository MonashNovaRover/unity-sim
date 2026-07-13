{ 
  pkgs ? import <nixpkgs> {
    config.allowUnfreePredicate = pkg: builtins.elem (pkgs.lib.getName pkg) [
      "corefonts"
    ];
    config.permittedInsecurePackages = [
      "dotnet-sdk-6.0.428"
      "dotnet-runtime-6.0.36"
    ];
  } 
}:

let
  version = "3.13.1"; # Match unityhub version

  dotnetPkg =
    (with pkgs.dotnetCorePackages; combinePackages [
      sdk_8_0
    ]);

  fhsEnvName = "nova-unityhub-fhs-env";

  fhsBashrcDefinition = pkgs.writeShellScriptBin "fhs-bashrc" ''
    # Source parent .bashrc if it exists (optional)
    if [ -f ~/.bashrc ]; then
      source ~/.bashrc
    fi

    # Your custom setup here
    alias hub='unityhub --no-sandbox'
    alias unity='~/Unity/Hub/Editor/6000.1.14f1/Editor/Unity -projectpath ./unity-sim'

    export DOTNET_ROOT=${dotnetPkg}

    echo ""
    echo "Entered Unity FHS environment."
    echo ""
    echo "Type:"
    echo " - 'unity' to open the editor"
    echo " - 'hub' to open unity hub"
    echo " - 'rider' to open Jetbrains rider"
    echo ""
  '';
  fhsBashrc = fhsBashrcDefinition + "/bin/fhs-bashrc";

  # stolen from https://github.com/NixOS/nixpkgs/blob/master/pkgs/by-name/un/unityhub/package.nix
  fhsEnv = pkgs.buildFHSEnv rec {
    pname = fhsEnvName;
    inherit version;
    

    targetPkgs =
      pkgs:
      with pkgs;
      [
        # Unity Hub binary dependencies
        xorg.libXrandr
        xdg-utils

        # GTK filepicker
        gsettings-desktop-schemas
        hicolor-icon-theme

        # Bug Reporter dependencies
        fontconfig
        freetype
        lsb-release
      ]
      ++ [
        # ! Extra packages added for the shell.nix
        tmux

        # For your IDE
        dotnetPkg
        mono
      
        powershell
      ];

    multiPkgs =
      pkgs:
      with pkgs;
      [
        # Unity Hub ldd dependencies
        cups
        gtk3
        expat
        libxkbcommon
        lttng-ust_2_12
        krb5
        alsa-lib
        nss
        libdrm
        libgbm
        nspr
        atk
        dbus
        at-spi2-core
        pango
        xorg.libXcomposite
        xorg.libXext
        xorg.libXdamage
        xorg.libXfixes
        xorg.libxcb
        xorg.libxshmfence
        xorg.libXScrnSaver
        xorg.libXtst

        # Unity Hub additional dependencies
        libva
        openssl
        cairo
        libnotify
        libuuid
        libsecret
        udev
        libappindicator
        wayland
        cpio
        icu
        libpulseaudio

        # Unity Editor dependencies
        libglvnd # provides ligbl
        xorg.libX11
        xorg.libXcursor
        glib
        gdk-pixbuf
        (libxml2.overrideAttrs (oldAttrs: rec {
          version = "2.13.8";
          src = fetchurl {
            url = "mirror://gnome/sources/libxml2/${lib.versions.majorMinor version}/libxml2-${version}.tar.xz";
            hash = "sha256-J3KUyzMRmrcbK8gfL0Rem8lDW4k60VuyzSsOhZoO6Eo=";
          };
          patches = [];
          meta = oldAttrs.meta // {
            # knownVulnerabilities = oldAttrs.meta.knownVulnerabilities or [ ] ++ [
            #   "CVE-2025-6021"
            # ];
          };
        }))
        zlib
        clang
        git # for git-based packages in unity package manager

        # Unity Editor 6000 specific dependencies
        harfbuzz
        vulkan-loader

        # Unity Bug Reporter specific dependencies
        xorg.libICE
        xorg.libSM

        # Fonts used by built-in and third party editor tools
        corefonts
        dejavu_fonts
        liberation_ttf

        msbuild
      ];

    runScript = ''
      bash --rcfile ${fhsBashrc} -i 
    '';
  };

in 
pkgs.mkShell {
  buildInputs = [
    pkgs.tmux
    fhsEnv
    pkgs.msbuild
    pkgs.mono
    dotnetPkg
  ];

  shellHook = ''
    echo "${fhsEnv}/bin/${fhsEnvName}"
    exec ${fhsEnv}/bin/${fhsEnvName}
  '';
}

# alias hub='tmux new -A -s hub "unityhub --no-sandbox"'
      # alias unity='tmux new -A -s unity "~/Unity/Hub/Editor/6000.1.14f1/Editor/Unity -projectpath ./unity-sim"'
#

  #  -c '
  #       echo ""
  #       echo "Entered Unity FHS environment."
  #       export DOTNET_ROOT=${dotnetPkg}
  #       echo "  dotnet=$(which dotnet)"
  #       echo "  mono=$(which mono)"
  #       echo "  DOTNET_ROOT=$DOTNETROOT"
  #       echo ""
  #       echo "Type `unity` to open the editor in tmux."
  #       echo ""
        
  #       exec $SHELL
  #     '