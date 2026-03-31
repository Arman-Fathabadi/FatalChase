# FatalChase

FatalChase is a Unity driving survival game set in a night-time city. The player controls a supermini car, avoids police pursuit, manages damage and speed, and tries to stay alive long enough to win.

## Overview

The core objective is simple:

- Survive for 2 minutes to win
- Avoid getting busted or wrecked

The project was built as a playable MVP with a full win/lose loop, UI feedback, police pursuit, damage, audio, and a Windows build target.

## Features

- Physics-based driving with wheel colliders
- Police pursuit system with siren audio and chase behavior
- Survival timer win condition
- Health and damage system
- Collision feedback, smoke, and vehicle damage visuals
- HUD with speed, NOS, health, timer, and objective text
- Game over and win screens
- Background music and police siren audio

## Controls

- `W` / `Up Arrow`: accelerate
- `S` / `Down Arrow`: brake / reverse
- `A` / `Left Arrow`: steer left
- `D` / `Right Arrow`: steer right
- `Space`: handbrake
- `Left Shift`: NOS
- `R`: reset after win or loss

## Win And Lose Conditions

### Win

- Survive for 2 minutes

### Lose

- Get busted by the police
- Wreck the car by taking too much damage

## Project Structure

The most important Unity project folders are:

- `Assets/`
- `Packages/`
- `ProjectSettings/`

Main gameplay logic lives under:

- `Assets/Scripts/`

Main playable city scene:

- `Assets/Versatile Studio Assets/Demo City By Versatile Studio/Scenes/demo_city_night.unity`

## How To Open The Project

1. Open the project in Unity 6
2. Load the main scene:
   `Assets/Versatile Studio Assets/Demo City By Versatile Studio/Scenes/demo_city_night.unity`
3. Press Play in the Unity Editor

## Build Target

This project is intended to build for:

- Windows 64-bit

## Git LFS

This repository uses Git LFS for a few large project assets. If you clone the project, make sure Git LFS is installed before opening the project.

## Tech Stack

- Unity 6
- C#
- Unity Input System
- WheelCollider-based vehicle physics

## Author

Arman Fathabadi
