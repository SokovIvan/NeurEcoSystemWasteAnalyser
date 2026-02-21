import  tensorflow as tf
import keras
import socket
import json
import base64
from PIL import Image
import numpy as np
from matplotlib import pyplot as plt
import os
import pandas as pd
import numpy as np
from keras.src.saving import load_model
from keras.src.utils import load_img, img_to_array
from tensorflow import keras
from tensorflow.keras import layers
from sklearn.model_selection import train_test_split
import matplotlib.pyplot as plt

INPUT_IMAGE_SIZE = (512, 512)

def load_data(data_dir):
    images = []
    labels = []
    sensor_data = []

    excel_file = os.path.join(data_dir, 'Данные.xlsx')
    df = pd.read_excel(excel_file, sheet_name=None)

    for sheet_name, sheet_data in df.items():
        print(sheet_name)
        sheet_data = sheet_data.iloc[2:]

        for index, row in sheet_data.iterrows():

            label = 1 if str(row[0]).isdigit() else 0
            labels.append(label)

            img1_path = os.path.join(data_dir, sheet_name, 'Фото', f"{row[0]}.jpg")
            img2_path = os.path.join(data_dir, sheet_name, 'Тепловизор', f"{row[0]}.bmp")

            img1 = load_img(img1_path, target_size=INPUT_IMAGE_SIZE)
            img2 = load_img(img2_path, target_size=INPUT_IMAGE_SIZE)

            img1 = img_to_array(img1) / 255.0
            img2 = img_to_array(img2) / 255.0

            images.append((img1, img2))
            sensor_data.append(row[1:5].values)

    return np.array(images), np.array(labels), np.array(sensor_data)
# Загрузите модель из файла h5d
model = keras.models.load_model('pollution_detection_model.h5')
data_dir = 'данные'
images, labels, sensor_data = load_data(data_dir)

# Make predictions
predictions = []
for image_pair, sensor_values in zip(images, sensor_data):
    img1, img2 = image_pair
    print(sensor_values)
    # Ensure correct data types
    img1 = img1.astype(np.float32)
    img2 = img2.astype(np.float32)
    sensor_values = sensor_values.astype(np.float32)

    # Add a batch dimension
    img1 = np.expand_dims(img1, axis=0)
    img2 = np.expand_dims(img2, axis=0)
    sensor_values = np.expand_dims(sensor_values, axis=0)

    # Prepare inputs for the model
    input_data = {
        'input_image_1': img1,
        'input_image_2': img2,
        'input_sensors': sensor_values
    }
    #print(type(img1), img1.dtype)
    #print(type(img2), img2.dtype)
    #print(type(sensor_values), sensor_values.dtype)
    # Make the prediction
    prediction = model.predict(input_data)
    predictions.append(prediction)

# Process predictions
for i, prediction in enumerate(predictions):
    # ... (interpret prediction and print results)
    print(f"Image {i+1}: Prediction = {prediction}, Sensor Data = {sensor_data[i]}")
