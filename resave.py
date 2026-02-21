from tensorflow import keras

# Загрузите модель из файла h5d
model = keras.models.load_model('pollution_detection_model.h5')

# Сохраните модель в формате SavedModel
model.export('pollution_detection_model',format="tf")