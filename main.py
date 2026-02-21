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


def create_model():
    input_image_1 = keras.Input(shape=(512, 512, 3), name='input_image_1')
    input_image_2 = keras.Input(shape=(512, 512, 3), name='input_image_2')
    input_sensors = keras.Input(shape=(4,), name='input_sensors')

    x1 = layers.Conv2D(32, (3, 3), activation='relu', padding='same')(input_image_1)
    x1 = layers.MaxPooling2D((2, 2))(x1)
    x1 = layers.Conv2D(64, (3, 3), activation='relu', padding='same')(x1)
    x1 = layers.MaxPooling2D((2, 2))(x1)

    x2 = layers.Conv2D(32, (3, 3), activation='relu', padding='same')(input_image_2)
    x2 = layers.MaxPooling2D((2, 2))(x2)
    x2 = layers.Conv2D(64, (3, 3), activation='relu', padding='same')(x2)
    x2 = layers.MaxPooling2D((2, 2))(x2)

    input_sensors_expanded = layers.Dense(64)(input_sensors)
    input_sensors_reshaped = layers.Reshape((1, 1, 64))(input_sensors_expanded)

    input_sensors_upsampled = layers.UpSampling2D(size=(128, 128))(input_sensors_reshaped)

    merged = layers.Concatenate()([x1, x2, input_sensors_upsampled])
    x = layers.Flatten()(merged)
    x = layers.Dense(128, activation='relu')(x)
    x = layers.Dense(1, activation='sigmoid', name='classification')(x)

    model = keras.Model(inputs=[input_image_1, input_image_2, input_sensors], outputs=x)
    return model

data_dir = 'данные'
images, labels, sensor_data = load_data(data_dir)

X_train, X_test, y_train, y_test, sensor_train, sensor_test = train_test_split(
    images, labels, sensor_data, test_size=0.2, random_state=42
)

model = create_model()
#model=load_model("pollution_detection_model.h5")
model.compile(optimizer='adam', loss='binary_crossentropy', metrics=['accuracy'])

X_train = X_train.astype('float32')
sensor_train = sensor_train.astype('float32')

history = model.fit([X_train[:, 0], X_train[:, 1], sensor_train], y_train, epochs=15, batch_size=16, validation_split=0.2)

model.save('pollution_detection_model.h5')

plt.plot(history.history['accuracy'], label='Accuracy on education')
plt.plot(history.history['val_accuracy'], label='Accuracy on validation')
plt.xlabel('Epoch')
plt.ylabel('Accuracy')
plt.legend()
plt.title('Model learning')
plt.show()
