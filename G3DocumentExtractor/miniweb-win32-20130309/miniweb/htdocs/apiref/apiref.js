function addVersionsToCombo(versionSelect, versions)
{
  for (var i = 0; i < versions.length; i++) {
    var option = document.createElement("option");
    option.text = versions[i];
//    $(option).prop('selected', true)
    versionSelect.append(option);
  }

}

function loadVersions(docVersion, compVersion, div) {
  $.getJSON("doc-index.json", function(data, s, xhr) {
    addVersionsToCombo(docVersion, data.versions)
    addVersionsToCombo(compVersion, data.versions)
    var last = data.versions[data.versions.length-1];
    var prev = data.versions[data.versions.length-2];
    docVersion.val(last)
    compVersion.val(prev)
    loadDocumentation(last, prev, div)
  });
}

function loadDocumentation(docVersion, compVersion, div) {
  $(div).empty();
  $.getJSON(docVersion+".json", function(docData) {
    $.getJSON(compVersion+".json", function(compData) {
      div.append(createTextDiv("header", "Documentation for Tobii Pro Glasses 3"));
      div.append(createTextDiv("header", "Firmware version " + docVersion));
      $.each(docData, function(key, value) {
        AddObject(div, key, value, compData[key]);
      });
    });
  });
}

function AddObject(parent, key, obj, compObj) {
  var objDiv = createDiv("object")

  objDiv.append(createTextDiv("objectHeader", "Object"));

  if (compObj == undefined) {
    objDiv.addClass("new");
    compObj = {properties: {}, actions: {}, signals: {}};
  }

  objDiv.append(createTextDiv("objectName", key));

  var hasContent = AddProperties(objDiv, obj.properties, compObj.properties);
  hasContent |= AddActions(objDiv, obj.actions, compObj.actions);  
  hasContent |= AddSignals(objDiv, obj.signals, compObj.signals);

  if (hasContent) {
    parent.append(objDiv);  
  }
}

function createDiv(cls)
{
  var div = $(document.createElement("div"));
  div.addClass(cls);
  return div;
}

function createTextDiv(cls, text) {
  var res = createDiv(cls);
  res.text(text);
  return res;
}

function AddProperties(parent, properties, compProperties) {
  var res = false;
  var propertiesDiv = createDiv("properties");

  propertiesDiv.append(createTextDiv("propertiesHeader", "Properties"));

  $.each(properties, function(name, prop) {
    if (name != "name") {
      
      var propDiv = createDiv("property");

      flagNewModified(prop, name, compProperties, propDiv);

      var propSignatureDiv = createDiv("propertySignature");
      propDiv.append(propSignatureDiv)

      var propModeDiv = createDiv("propertyMode");
      if(prop.mode == "r") {
        propModeDiv.text("readonly");
      } else if (prop.mode == "rw") {
        propModeDiv.text("read/write");
      }
      propDiv.append(propModeDiv);

      var propType = prop.type;
      if (prop.range.length > 0 && prop.type != "boolean")
      {
        if (propType == "int" && prop.range.length == 2 && prop.range[0] == 0 && prop.range[1] == 4294967295) {
          propType = "int32";
        } else if (propType == "int" && prop.range.length == 2 && prop.range[0] == 0 && prop.range[1] == 65535) {
          propType = "int16";
        } else {
          var separator = ", ";
          if ((prop.type == "int" || prop.type == "real") && prop.range.length == 2) {
            separator = "..";
          }

          var rangeDiv = createDiv("propertyRange")
          rangeDiv.append(createTextDiv("propertyRangeHeader", "Range:"));
          rangeDiv.append(createTextDiv("propertyRangeInfo", prop.range.join(separator)));
          propDiv.append(rangeDiv);
        }
      }

      propSignatureDiv.text("."+name + ": " + propType);

      propDiv.append(createDocDiv("propertyDoc", prop.help));

      propertiesDiv.append(propDiv);
      res = true;
    }
  })
  if (res)
    parent.append(propertiesDiv);

  return res;
}

function AddActions(parent, actions, compActions) {
  var res = false;
  var actionsDiv = createDiv("actions");
   
  actionsDiv.append(createTextDiv("actionsHeader", "Actions"));

  $.each(actions, function(name, action) {
      var actionDiv = createDiv("action");

      flagNewModified(action, name, compActions, actionDiv);

      var signature = "!"+name + "(" + action.args.join(", ") + ")" + ": " + action.return;
      actionDiv.append(createTextDiv("actionSignature", signature));

      actionDiv.append(createDocDiv("actionDoc", action.help));

      actionsDiv.append(actionDiv)
      res = true;
  });
  if (res) {
    parent.append(actionsDiv);
  }
  return res;
}

function flagNewModified(element, name, compElements, div) {
  if (compElements[name] == undefined) {
    div.addClass("new")
  } else if (JSON.stringify(compElements[name]) != JSON.stringify(element)) {
    div.addClass("modified")
  }
}

function createDocDiv(divName, doc)
{
  var rows = doc.split("\n");    
  var docDiv =  createDiv("description");
  docDiv.append(createTextDiv("docHeader", "Description:"))
  $.each(rows, function(i, row){
    var helpDiv = createDiv(divName);
    helpDiv.text(row);
    docDiv.append(helpDiv)
  });
  return docDiv;
}

function AddSignals(parent, signals, compSignals) {
  var res = false;
  var signalsDiv = createDiv("signals");

  signalsDiv.append(createTextDiv("signalsHeader", "Signals"));

  $.each(signals, function(name, signal) {
      var signalDiv = createDiv("signal");
      
      flagNewModified(signal, name, compSignals, signalDiv);

      var signature = ":"+name + " => (" + signal.args.join(", ") + ")";
      signalDiv.append(createTextDiv("signalSignature", signature));

      signalDiv.append(createDocDiv("signalDoc", signal.help));

      signalsDiv.append(signalDiv)
      res = true;
  });

  if (res)
    parent.append(signalsDiv);
  return res;
}
