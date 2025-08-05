# unity-sim
A Unity based simulator for Banksia

> Current unity version: `Unity 6.1 (6000.1.14f1)`

## Development

You'll need to get Unity through Unity Hub. 

### 1. Installing `unityhub` on NixOS

Add `pkgs.unityhub` and an appropriate IDE to your configuration.nix:

```nix
  # /etc/nixos/configuration.nix
  # ...
  home-manager.users.nova = {
    home.packages = with pkgs; [
      # ...

      # Add here:
      unityhub
      jetbrains.rider  # or another appropriate IDE
    ];
    
    #...
  };
  # ...
};
# Or you could also add it to environment.systemPackages, but it is better to do it with home-manager
```

I recommend using Jetbrains Rider for Unity development.

> [!IMPORTANT]  
> Make sure you update your nix channels (there was an important fix 5 hours before I wrote this)
> ```sh
> sudo nix-channels --update
> ```

Rebuild your system (this might take a while, I'm sorry).

```sh
sudo nixos-rebuild switch
```

<img width="33%" alt="image" src="https://github.com/user-attachments/assets/53b0e7ae-d93b-4dea-8aea-5c25f7647bed" align="right" />

> [!NOTE]  
> Unity Hub won't work through a `nix-shell`, as it needs to use `xdg-open` from your web browser to be able to sign in.

### 2. Downloading Unity

Run `unityhub`, and press the appropriate button to sign in or create an account using a web browser.

Once in, you may be prompted to download the latest version of unity. Dismiss this, and manually select a version of Unity to download that matches the current project (See the top of the README for the current version). 

On the left side-bar, select `Installs`. Then, in the top left, select `Install Editor`. Find the closest version >= to the version listed in the README, and press `Install`.

<img width="100%" height="719" alt="image" src="https://github.com/user-attachments/assets/add8c5ba-c681-4b64-9f40-df33c574e559" />

### 3. Open project

TODO


### How I created the project:

I make a shell.nix with a fhs to allow us to run dynamically linked executables of the unity editor.

```sh
# at the root of the repository
nix-shell

tmux new -s hub

# in [hub]
unityhub
# ctrl + b, d
```

In a new terminal window:
```sh
tail -f ~/.config/unityhub/logs/info-log.json
```

Then create the project in unity hub. You should see a message in the logs like:
```
{"timestamp":"2025-08-05T08:44:47.682Z","level":"info","moduleName":"LaunchProcess","pid":16374,"message":"Spawning editor instance with command:  /home/nova/Unity/Hub/Editor/6000.1.14f1/Editor/Unity , and arguments:  [ '-createproject', '/home/nova/code/unity-sim/Simulator/JoeM', '-cloneFromTemplate', '/home/nova/Unity/Hub/Editor/6000.1.14f1/Editor/Data/Resources/PackageManager/ProjectTemplates/com.unity.template.3d-cross-platform-17.0.14.tgz', '-cloudOrganization', '1324567', '-cloudProject', 'AAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA', '-cloudEnvironment', 'production', '-useHub', '-hubIPC', '-hubSessionId', 'AAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA', '-accessToken', 'AAAAAAAAAAAAAAAAAAAA_AAAAAAAAAAAAAA_AAAAAAAAAAAA' ]"}
```

Copy the command it tried to run:
```
/home/nova/Unity/Hub/Editor/6000.1.14f1/Editor/Unity , and arguments:  [ '-createproject', '/home/nova/code/unity-sim/Simulator/JoeM', '-cloneFromTemplate', '/home/nova/Unity/Hub/Editor/6000.1.14f1/Editor/Data/Resources/PackageManager/ProjectTemplates/com.unity.template.3d-cross-platform-17.0.14.tgz', '-cloudOrganization', '1324567', '-cloudProject', 'AAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA', '-cloudEnvironment', 'production', '-useHub', '-hubIPC', '-hubSessionId', 'AAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA', '-accessToken', 'AAAAAAAAAAAAAAAAAAAA_AAAAAAAAAAAAAA_AAAAAAAAAAAA'
```

Then in the original shell, make a new tmux session, and run the command unityhub tried, formatting the arguments properly:
```sh
# in original shell
tmux new -s editor

# in [editor]
/home/nova/Unity/Hub/Editor/6000.1.14f1/Editor/Unity -createproject /home/nova/code/unity-sim/Simulator/JoeM -cloneFromTemplate /home/nova/Unity/Hub/Editor/6000.1.14f1/Editor/Data/Resources/PackageManager/ProjectTemplates/com.unity.template.3d-cross-platform-17.0.14.tgz -cloudOrganization 1324567 -cloudProject AAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA -cloudEnvironment production -useHub -hubIPC -hubSessionId AAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA -accessToken AAAAAAAAAAAAAAAAAAAA_AAAAAAAAAAAAAA_AAAAAAAAAAAA
```

Then, the editor loaded:

<p align="center">
  <img width="800px" alt="image" src="https://github.com/user-attachments/assets/4018a747-41f8-48fb-b536-2986ed273f04" />
</p>
