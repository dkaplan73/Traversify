# MyUnityProject Documentation

## Project Overview
MyUnityProject is a Unity-based project designed to create an engaging RPG map tool. This project includes essential scripts and scene data to facilitate the development of RPG maps.

## Project Structure
The project is organized into the following main directories:

- **Assets**: Contains all the game assets, including scenes and scripts.
  - **Scenes**: Holds the Unity scene files.
    - `Main.unity`: The main scene of the project, which includes all game objects and their properties.
  - **Scripts**: Contains the scripts used in the project.
    - **RPGMapTool**: A folder dedicated to RPG map tools.
      - **Utilities**: Contains utility scripts for various functionalities.
        - `ColorUtils.cs`: Provides utility functions related to color manipulation.

- **ProjectSettings**: Contains project configuration settings necessary for Unity.
  - `ProjectSettings.asset`: Includes various project settings such as player and editor configurations.

## Setup Instructions
1. **Clone the Repository**: Clone the project repository to your local machine.
2. **Open in Unity**: Open the project in the Unity Editor.
3. **Load the Scene**: Navigate to the `Assets/Scenes` directory and open `Main.unity` to view the main scene.
4. **Run the Project**: Press the play button in the Unity Editor to run the project.

## Usage
- The `ColorUtils` class provides a method to blend colors, which can be utilized in various parts of the project where color manipulation is needed.
- To use the `BlendColors` method, simply call it with the desired base color, blend color, and alpha value.

## Contribution
Contributions to the project are welcome. Please fork the repository and submit a pull request with your changes.

## License
This project is licensed under the MIT License. See the LICENSE file for more details.