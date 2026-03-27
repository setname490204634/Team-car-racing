#!/bin/bash
#SBATCH --job-name=TeamCarRacingMARL
#SBATCH --output=output_%j.txt
#SBATCH --ntasks=1
#SBATCH --time=01:00:00
#SBATCH --mem=4G

cd ~/teamCarRacing/Team-car-racing/pythonSide
python RllibMARLstarter.py