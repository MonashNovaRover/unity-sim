{ pkgs ? import <nixpkgs> {} }:

let
  version = "3.13.1"; # Match unityhub version

  # stolen from https://github.com/NixOS/nixpkgs/blob/master/pkgs/by-name/un/unityhub/package.nix
  fhsEnv = pkgs.buildFHSEnv {
    pname = "nova-unityhub-fhs-env";
    inherit version;
    runScript = "bash";

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
      ];
  };

in 
  fhsEnv.env