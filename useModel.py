import io
import json
import socket
import pickle

import cv2
import  tensorflow as tf
import keras
import socket
import json
import base64
from PIL import Image
import numpy as np
from matplotlib import pyplot as plt

model = keras.models.load_model('pollution_detection_model.h5')
def predict(input_data):
    image1,image2, sensor_data =input_data

    image1 = tf.expand_dims(image1, axis=0)
    image2 = tf.expand_dims(image2, axis=0)
    sensor_data = tf.expand_dims(sensor_data, axis=0)

    image1 = tf.expand_dims(image1, axis=-1)
    image1 = tf.image.grayscale_to_rgb(image1)
    image2 = tf.expand_dims(image2, axis=-1)
    image2 = tf.image.grayscale_to_rgb(image2)


    pred = model.predict([image1, image2, sensor_data])
    print(pred)

    return float(pred[0][0])

while True:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.bind(('localhost', 12345))
    s.listen(1)
    print("Ожидание подключения...")
    conn, addr = s.accept()
    print(f"Подключено к {addr}")
    with conn:
        print(f'Connected by {addr}')
        data = b""
        while True:
            part = conn.recv(4096)
            if not part:
                conn.close()
                s.close()
                break
            data += part
            try:
                data_received = json.loads(data.decode('utf-8'))
                images = data_received['Images']
                numbers = data_received['Numbers']
                img1 = np.array(images[0]).reshape((512, 512))
                img2 = np.array(images[1]).reshape((512, 512))
                input_data = img1,img2,numbers
                prediction = predict(input_data)
                print(prediction)
                data_to_send = {
                    'prediction': prediction
                }
                conn.sendall(json.dumps(data_to_send).encode("utf-8"))
                print("Numbers received:", numbers)
            except json.JSONDecodeError as e:
                pass